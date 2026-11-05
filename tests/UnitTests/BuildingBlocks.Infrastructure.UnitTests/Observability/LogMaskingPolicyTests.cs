using Advocacia.BuildingBlocks.Infrastructure.Observability;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Observability;

public class LogMaskingPolicyTests
{
    // Formato clássico de um token FCM: "<id>:APA91b<corpo>".
    private static readonly string FcmToken = "dQw4w9WgXcQ:APA91b" + new string('x', 120) + "_-Zy";

    [Fact]
    public void Mask_HidesAClassicFcmToken()
    {
        var masked = LogMaskingPolicy.Mask($"Falha ao enviar para {FcmToken} (timeout)");

        masked.Should().NotContain("APA91b").And.NotContain("dQw4w9WgXcQ");
        masked.Should().Be("Falha ao enviar para ***FCM-TOKEN*** (timeout)");
    }

    [Fact]
    public void Mask_StillMasksTheOtherSensitiveData()
    {
        LogMaskingPolicy.Mask("CPF 529.982.247-25 e e-mail ana.souza@escritorio.com.br")
            .Should().Be("CPF ***.***.***-** e e-mail an***@escritorio.com.br");
    }

    [Fact]
    public void Mask_LeavesOrdinaryTextAlone()
    {
        LogMaskingPolicy.Mask("Dispositivo 8d6b1f0e removido.").Should().Be("Dispositivo 8d6b1f0e removido.");
    }
}
