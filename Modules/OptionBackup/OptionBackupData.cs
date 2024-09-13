using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR;

public class OptionBackupData
{
    public List<OptionBackupValue> AllValues;

    public OptionBackupData(IGameOptions option)
    {
        AllValues = new(32);

        foreach (var name in Enum.GetValues<ByteOptionNames>())
        {
            if (option.TryGetByte(name, out var value))
                AllValues.Add(new ByteOptionBackupValue(name, value));
        }

        foreach (var name in Enum.GetValues<BoolOptionNames>())
        {
            if (option.TryGetBool(name, out var value) && name != BoolOptionNames.GhostsDoTasks)
                AllValues.Add(new BoolOptionBackupValue(name, value));
        }

        foreach (var name in Enum.GetValues<FloatOptionNames>())
        {
            if (option.TryGetFloat(name, out var value))
                AllValues.Add(new FloatOptionBackupValue(name, value));
        }

        foreach (var name in Enum.GetValues<Int32OptionNames>())
        {
            if (option.TryGetInt(name, out var value))
                AllValues.Add(new IntOptionBackupValue(name, value));
        }

        // [Vanilla bug] Get the number of people in the room separately, since GetInt cannot get the number of people in the room
        AllValues.Add(new IntOptionBackupValue(Int32OptionNames.MaxPlayers, option.MaxPlayers));
        // Since TryGetUInt is not implemented, get it separately
        AllValues.Add(new UIntOptionBackupValue(UInt32OptionNames.Keywords, (uint)option.Keywords));

        RoleTypes[] array = [RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.GuardianAngel, RoleTypes.Shapeshifter, RoleTypes.Noisemaker, RoleTypes.Phantom, RoleTypes.Tracker];
        foreach (RoleTypes role in array)
        {
            AllValues.Add(new RoleRateBackupValue(role, option.RoleOptions.GetNumPerGame(role), option.RoleOptions.GetChancePerGame(role)));
        }
    }

    public IGameOptions Restore(IGameOptions option)
    {
        AllValues.ForEach(o => o.Restore(option));
        return option;
    }

    public byte GetByte(ByteOptionNames name) => Get<ByteOptionNames, byte>(name);
    public bool GetBool(BoolOptionNames name) => Get<BoolOptionNames, bool>(name);
    public float GetFloat(FloatOptionNames name) => Get<FloatOptionNames, float>(name);
    public int GetInt(Int32OptionNames name) => Get<Int32OptionNames, int>(name);
    public uint GetUInt(UInt32OptionNames name) => Get<UInt32OptionNames, uint>(name);

    public TValue Get<TKey, TValue>(TKey name)
        where TKey : Enum
    {
        var value = AllValues
            .OfType<OptionBackupValueBase<TKey, TValue>>()
            .FirstOrDefault(val => val.OptionName.Equals(name));

        return value == null ? default : value.Value;
    }
}