using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;

namespace EHR;

public class WriterProxy(MessageWriter writer) : ICustomWriter
{
    public MessageWriter Writer { get; private set; } = writer;

    public void Write(float val) => Writer.Write(val);
    public void Write(string val) => Writer.Write(val);
    public void Write(ulong val) => Writer.Write(val);
    public void Write(int val) => Writer.Write(val);
    public void Write(uint val) => Writer.Write(val);
    public void Write(ushort val) => Writer.Write(val);
    public void Write(byte val) => Writer.Write(val);
    public void Write(sbyte val) => Writer.Write(val);
    public void Write(bool val) => Writer.Write(val);
    public void Write(Il2CppStructArray<byte> bytes) => Writer.Write(bytes);
    public void Write(Il2CppStructArray<byte> bytes, int offset, int length) => Writer.Write(bytes, offset, length);
    public void WriteBytesAndSize(Il2CppStructArray<byte> bytes) => Writer.WriteBytesAndSize(bytes);
    public void WritePacked(int val) => Writer.WritePacked(val);
    public void WritePacked(uint val) => Writer.WritePacked(val);
    public void WriteNetObject(InnerNetObject obj) => Writer.WriteNetObject(obj);
}