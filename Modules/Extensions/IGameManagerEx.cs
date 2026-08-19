using AmongUs.GameOptions;

namespace EHR.Modules.Extensions;

public static class IGameManagerEx
{
    extension(BoolOptionNames name)
    {
        public void Set(bool value, IGameOptions opt)
        {
            opt.SetBool(name, value);
        }

        public void Set(bool value, NormalGameOptionsV11 opt)
        {
            if (name is not BoolOptionNames.GhostsDoTasks and not BoolOptionNames.Roles)
                opt.SetBool(name, value);
        }

        public void Set(bool value, HideNSeekGameOptionsV11 opt)
        {
            opt.SetBool(name, value);
        }
    }

    extension(Int32OptionNames name)
    {
        public void Set(int value, IGameOptions opt)
        {
            opt.SetInt(name, value);
        }

        public void Set(int value, NormalGameOptionsV11 opt)
        {
            opt.SetInt(name, value);
        }

        public void Set(int value, HideNSeekGameOptionsV11 opt)
        {
            opt.SetInt(name, value);
        }
    }

    extension(FloatOptionNames name)
    {
        public void Set(float value, IGameOptions opt)
        {
            opt.SetFloat(name, value);
        }

        public void Set(float value, NormalGameOptionsV11 opt)
        {
            opt.SetFloat(name, value);
        }

        public void Set(float value, HideNSeekGameOptionsV11 opt)
        {
            opt.SetFloat(name, value);
        }
    }

    extension(ByteOptionNames name)
    {
        public void Set(byte value, IGameOptions opt)
        {
            opt.SetByte(name, value);
        }

        public void Set(byte value, NormalGameOptionsV11 opt)
        {
            opt.SetByte(name, value);
        }

        public void Set(byte value, HideNSeekGameOptionsV11 opt)
        {
            opt.SetByte(name, value);
        }
    }

    extension(UInt32OptionNames name)
    {
        public void Set(uint value, IGameOptions opt)
        {
            opt.SetUInt(name, value);
        }

        public void Set(uint value, NormalGameOptionsV11 opt)
        {
            opt.SetUInt(name, value);
        }

        public void Set(uint value, HideNSeekGameOptionsV11 opt)
        {
            opt.SetUInt(name, value);
        }
    }
}