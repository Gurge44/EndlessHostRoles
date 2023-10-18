using AmongUs.GameOptions;
using System;
using System.Collections.Generic;
using System.Linq;


namespace TOHE;

public class OptionBackupData
{
    public List<OptionBackupValue> AllValues;
    public OptionBackupData(IGameOptions option)
    {
        AllValues = new(32);

        System.Collections.IList list = Enum.GetValues(typeof(ByteOptionNames));
        for (int i = 0; i < list.Count; i++)
        {
            ByteOptionNames name = (ByteOptionNames)list[i];
            if (option.TryGetByte(name, out var value))
                AllValues.Add(new ByteOptionBackupValue(name, value));
        }
        System.Collections.IList list1 = Enum.GetValues(typeof(BoolOptionNames));
        for (int i = 0; i < list1.Count; i++)
        {
            BoolOptionNames name = (BoolOptionNames)list1[i];
            if (option.TryGetBool(name, out var value) && name != BoolOptionNames.GhostsDoTasks)
                AllValues.Add(new BoolOptionBackupValue(name, value));
        }
        System.Collections.IList list2 = Enum.GetValues(typeof(FloatOptionNames));
        for (int i = 0; i < list2.Count; i++)
        {
            FloatOptionNames name = (FloatOptionNames)list2[i];
            if (option.TryGetFloat(name, out var value))
                AllValues.Add(new FloatOptionBackupValue(name, value));
        }
        System.Collections.IList list3 = Enum.GetValues(typeof(Int32OptionNames));
        for (int i = 0; i < list3.Count; i++)
        {
            Int32OptionNames name = (Int32OptionNames)list3[i];
            if (option.TryGetInt(name, out var value))
                AllValues.Add(new IntOptionBackupValue(name, value));
        }
        // [バニラ側バグ] GetIntで部屋の人数のみ取得できないため、別で取得する
        AllValues.Add(new IntOptionBackupValue(Int32OptionNames.MaxPlayers, option.MaxPlayers));
        // TryGetUIntが実装されていないため、別で取得する
        AllValues.Add(new UIntOptionBackupValue(UInt32OptionNames.Keywords, (uint)option.Keywords));

        RoleTypes[] array = new RoleTypes[] { RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.GuardianAngel, RoleTypes.Shapeshifter };
        for (int i = 0; i < array.Length; i++)
        {
            RoleTypes role = array[i];
            AllValues.Add(new RoleRateBackupValue(role, option.RoleOptions.GetNumPerGame(role), option.RoleOptions.GetChancePerGame(role)));
        }
    }

    public IGameOptions Restore(IGameOptions option)
    {
        for (int i = 0; i < AllValues.Count; i++)
        {
            OptionBackupValue value = AllValues[i];
            value.Restore(option);
        }
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
            .Where(val => val.OptionName.Equals(name)).
            FirstOrDefault();

        return value == null ? default : value.Value;
    }
}