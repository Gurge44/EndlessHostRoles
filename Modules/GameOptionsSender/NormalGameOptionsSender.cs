using AmongUs.GameOptions;
using System;

namespace EHR.Modules;

public class NormalGameOptionsSender : GameOptionsSender
{
    public override IGameOptions BasedGameOptions =>
        GameOptionsManager.Instance.CurrentGameOptions;

    public override bool IsDirty
    {
        get
        {
            try
            {
                if (_logicOptions == null || !GameManager.Instance.LogicComponents.Contains(_logicOptions))
                {
                    foreach (var glc in GameManager.Instance?.LogicComponents)
                        if (glc.TryCast<LogicOptions>(out var lo))
                            _logicOptions = lo;
                }

                return _logicOptions != null && _logicOptions.IsDirty;
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex.ToString(), "NormalGameOptionsSender.IsDirty.Get");
                return _logicOptions != null && _logicOptions.IsDirty;
            }
        }
        protected set { _logicOptions?.ClearDirtyFlag(); }
    }

    private LogicOptions _logicOptions;

    public override IGameOptions BuildGameOptions()
        => BasedGameOptions;
}