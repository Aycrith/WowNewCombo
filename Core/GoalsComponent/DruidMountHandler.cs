using System.Numerics;

using Core.Goals;

namespace Core;

public sealed class DruidMountHandler : IMountHandler
{
    private readonly MountHandler mountHandler;
    private readonly ClassConfiguration classConfig;
    private readonly CastingHandler castingHandler;
    private readonly PlayerReader playerReader;
    private readonly ConfigurableInput input;

    public DruidMountHandler(MountHandler mountHandler,
        CastingHandler castingHandler, ClassConfiguration classConfig,
        PlayerReader playerReader, ConfigurableInput input)
    {
        this.mountHandler = mountHandler;
        this.castingHandler = castingHandler;
        this.classConfig = classConfig;
        this.playerReader = playerReader;
        this.input = input;
    }

    public bool CanMount() =>
        mountHandler.CanMount();


    public void Dismount()
    {
        if (playerReader.Form is Form.Druid_Flight or Form.Druid_Travel)
        {
            for (int i = 0; i < classConfig.Form.Length; i++)
            {
                KeyAction form = classConfig.Form[i];
                if (form.FormEnum == playerReader.Form)
                {
                    input.Proc.KeyPress(form.ConsoleKey, input.defaultKeyPress);
                    return;
                }
            }
        }

        mountHandler.Dismount();
    }

    public bool IsMounted()
    {
        return
            playerReader.Form is Form.Druid_Flight or Form.Druid_Travel ||
            mountHandler.IsMounted();
    }

    public void MountUp()
    {
        for (int i = 0; i < classConfig.Form.Length; i++)
        {
            KeyAction keyAction = classConfig.Form[i];
            if (keyAction.FormEnum is Form.Druid_Flight or Form.Druid_Travel &&
                castingHandler.SwitchForm(keyAction))
            {
                return;
            }
        }

        mountHandler.MountUp();
    }

    public bool ShouldMount(Vector3 targetW) =>
        mountHandler.ShouldMount(targetW);
}
