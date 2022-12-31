using System;
using Microsoft.Diagnostics.DebugServices;
using System.Xml.Linq;
using Microsoft.Diagnostics.ExtensionCommands;
using Microsoft.Diagnostics.Runtime;
using System.Net;
using System.Data.SqlTypes;
using System.Collections.Generic;
using System.Reflection;
using System.Data;
using System.Drawing;

namespace Yudindm.Diagnostics.DumpExt
{
    [Command(Name = "yudindmdump", Aliases = new string[] { "yd" }, Help = "Test extension command.")]

    public class YudinDmDumpCommand : ExtensionCommandBase
    {
        public ClrRuntime Runtime { get; set; }
        public IMemoryService MemoryService { get; set; }

        [Option(Name = "--address", Aliases = new string[] { "-a" }, Help = "The address of a object.")]
        public string Address { get; set; }

        [Option(Name = "--fields", Aliases = new string[] { "-fl" }, Help = "Field list to show for object.")]
        public string FieldsToShow { get; set; }

        [Option(Name = "--verbose", Aliases = new string[] { "-v" }, Help = "Show all comments.")]
        public bool ShowAllComments { get; set; }

        [Option(Name = "--byMT", Aliases = new string[] { "-mt" }, Help = "Process all objects by MT.")]
        public string ByMT{ get; set; }

        protected override string GetDetailedHelp()
        {
            return
@"
--address,-a  The address of a object.
--byMT,-mt    Process all objects by MT.
--fields,-fl  Field list to show for object.
--verbose,-v  Many extra messages...
";
        }

        private List<string> _fieldNames = new List<string>();

        public override void ExtensionInvoke()
        {
            Verbose("YudinDm command invoked!");

            ParseFieldsToShow();

            Verbose($"Can Walk Heap: {Runtime.Heap.CanWalkHeap}");
            if (Runtime.Heap.CanWalkHeap)
            {
                ClrHeap heap = Runtime.Heap;

                ClrObject? cobject = TypeOfObjectByAddress(heap, Address);
                ShowObjectFields(heap, cobject);
                AllObjectsByMT(heap, ByMT);
            }

            Verbose("YudinDm command finished!");
        }

        private void ParseFieldsToShow()
        {
            if (string.IsNullOrEmpty(FieldsToShow)) return;
            _fieldNames.AddRange(FieldsToShow.Split(new char[] {','}));
        }

        private void AllObjectsByMT(ClrHeap heap, string byMT)
        {
            if (string.IsNullOrEmpty(byMT))
            {
                Verbose("ByMT is not specified...");
                return;
            }

            if (!TryParseAddress(byMT, out var uaddr))
            {
                WriteLine("Error: Hexadecimal address expected.");
                return;
            }

            ClrType ctype = Runtime.GetTypeByMethodTable(uaddr);
            if (ctype == null)
            {
                WriteLine("Error: Address is not MT.");
                return;
            }
            Verbose($"All objects of type: {ctype.Name}");
            foreach (ClrObject cobject in heap.EnumerateObjects())
            {
                if (cobject.Type.MethodTable == uaddr)
                {
                    Write($"{cobject.Address:X16} ");
                    ShowObjectFields(heap, cobject);
                }
            }
        }

        private void ShowObjectFields(ClrHeap heap, ClrObject? cobject)
        {
            if (cobject == null)
            {
                Verbose("No object resolved...");
                return;
            }

            foreach (var fieldname in _fieldNames)
            {
                var dispFieldname = fieldname.Replace("k__BackingField", "");
                ClrInstanceField field = cobject.Value.Type.GetFieldByName(fieldname);
                if (field == null)
                    Write($"{fieldname}");
                else if (field.IsPrimitive)
                {
                    MethodInfo miRead = typeof(ClrInstanceField).GetMethod("Read");
                    MethodInfo miReadG = miRead.MakeGenericMethod(Type.GetType($"System.{field.ElementType}"));
                    var strValue = miReadG.Invoke(field, new object[] { cobject.Value.Address, false }).ToString();
                    Write($"{dispFieldname}:{strValue}({field.ElementType}) ");
                }
                else if (field.IsObjectReference)
                {
                    ClrObject fcobject = cobject.Value.ReadObjectField(fieldname);
                    Write($"{dispFieldname}:{fcobject.Address:x16}({field.Type.Name}) "); 
                }
                else if (field.IsValueType)
                {
                    ClrValueType fcobject = cobject.Value.ReadValueTypeField(fieldname);
                    if (field.Type.Name == "System.DateTime")
                    {
                        ClrInstanceField dataField = fcobject.Type.GetFieldByName("_dateData");
                        ulong dateData = dataField.Read<ulong>(fcobject.Address, true);
                        ulong flags = dateData & 0xC000000000000000UL;
                        long ticks = (long)(dateData & 0x3FFFFFFFFFFFFFFFUL);
                        DateTimeKind kind = DateTimeKind.Unspecified;
                        if (flags == 0x4000000000000000UL) kind = DateTimeKind.Local;
                        if (flags == 0x8000000000000000UL) kind = DateTimeKind.Utc;
                        DateTime dt = new DateTime(ticks, kind);
                        Write($"{dispFieldname}:{dt:O} ");
                    }
                    else
                    {
                        Write($"{dispFieldname}:{DumpBytes(fcobject.Address, field.Size)}({field.Type.Name}) ");
                    }
                }
            }
            Write(Environment.NewLine);
        }

        private string DumpBytes(ulong address, int size)
        {
            string dumpBytes = "";
            if (MemoryService != null)
            {
                byte[] buffer = new byte[size];
                if (MemoryService.ReadMemory(address, buffer, out var bytesRead))
                {
                    for (var i = 0; i < bytesRead; i++)
                        dumpBytes += buffer[i].ToString("X2");
                }
            }
            return dumpBytes;
        }

        private ClrObject? TypeOfObjectByAddress(ClrHeap heap, string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                Verbose("Address is not specified...");
                return null;
            }

            if (!TryParseAddress(address, out var uaddr))
            {
                WriteLine("Error: Hexadecimal address expected...");
                return null;
            }

            ClrObject cobject = heap.GetObject(uaddr);

            if (_fieldNames.Count == 0)
            {
                ClrType ctype = cobject.Type;
                WriteLine($"The object's type name is: {ctype.Name}");

                List<(string Name, string TypeName)> fields = new List<(string Name, string TypeName)>();
                int width = 0;
                foreach (ClrInstanceField field in ctype.Fields)
                {
                    fields.Add((field.Name, field.Type.Name));
                    if (width < field.Name.Length) width = field.Name.Length;
                }
                foreach (var field in fields)
                    WriteLine($"{field.Name + new string(' ', width - field.Name.Length)} {field.TypeName}");
            }

            return cobject;
        }

        private void Verbose(string message)
        {
            if (!ShowAllComments) return;
            WriteLine(message);
        }
    }
}
