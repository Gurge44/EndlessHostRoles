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

        public void Set(bool value, NormalGameOptionsV10 opt)
        {
            if (name is not BoolOptionNames.GhostsDoTasks and not BoolOptionNames.Roles)
                opt.SetBool(name, value);
        }

        public void Set(bool value, HideNSeekGameOptionsV10 opt)
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

        public void Set(int value, NormalGameOptionsV10 opt)
        {
            opt.SetInt(name, value);
        }

        public void Set(int value, HideNSeekGameOptionsV10 opt)
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

        public void Set(float value, NormalGameOptionsV10 opt)
        {
            opt.SetFloat(name, value);
        }

        public void Set(float value, HideNSeekGameOptionsV10 opt)
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

        public void Set(byte value, NormalGameOptionsV10 opt)
        {
            opt.SetByte(name, value);
        }

        public void Set(byte value, HideNSeekGameOptionsV10 opt)
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

        public void Set(uint value, NormalGameOptionsV10 opt)
        {
            opt.SetUInt(name, value);
        }

        public void Set(uint value, HideNSeekGameOptionsV10 opt)
        {
            opt.SetUInt(name, value);
        }
    }
}