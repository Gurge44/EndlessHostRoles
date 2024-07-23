using System;
using AmongUs.GameOptions;

namespace EHR.Modules;

public sealed class NormalGameOptionsSender : GameOptionsSender
{
    private LogicOptions _logicOptions;
    private static IGameOptions BasedGameOptions => GameOptionsManager.Instance.CurrentGameOptions;

    protected override bool IsDirty
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
        set { _logicOptions?.ClearDirtyFlag(); }
    }

    protected override IGameOptions BuildGameOptions() => BasedGameOptions;
}