using Hazel;
using UnityEngine;

namespace EHR;

public static class HazelExtensions
{
    // -------------------------------------------------------------------------------------------------------------------------

    extension(MessageWriter writer)
    {
        public void Write(Vector2 vector)
        {
            NetHelpers.WriteVector2(vector, writer);
        }

        public void Write(Vector3 vector)
        {
            writer.Write(vector.x);
            writer.Write(vector.y);
            writer.Write(vector.z);
        }

        public void Write(Color color)
        {
            writer.Write(color.r);
            writer.Write(color.g);
            writer.Write(color.b);
            writer.Write(color.a);
        }
    }

    // -------------------------------------------------------------------------------------------------------------------------

    extension(MessageReader reader)
    {
        public Vector2 ReadVector2()
        {
            return NetHelpers.ReadVector2(reader);
        }

        public Vector3 ReadVector3()
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            return new(x, y, z);
        }

        public Color ReadColor()
        {
            float r = reader.ReadSingle();
            float g = reader.ReadSingle();
            float b = reader.ReadSingle();
            float a = reader.ReadSingle();
            return new(r, g, b, a);
        }
    }
}