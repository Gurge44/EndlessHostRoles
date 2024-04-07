using System;
using AmongUs.GameOptions;

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
                if (GameManager.Instance != null && GameManager.Instance.LogicComponents != null && (_logicOptions == null || !GameManager.Instance.LogicComponents.Contains(_logicOptions)))
                {
                    foreach (var glc in GameManager.Instance.LogicComponents)
                        if (glc.TryCast<LogicOptions>(out var lo))
                            _logicOptions = lo;
                }

                return _logicOptions is { IsDirty: true };
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "NormalGameOptionsSender.IsDirty.Get");
                return _logicOptions is { IsDirty: true };
            }
        }
        protected set { _logicOptions?.ClearDirtyFlag(); }
    }

    private LogicOptions _logicOptions;

    public override IGameOptions BuildGameOptions()
        => BasedGameOptions;
}