using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Advocacia.Modules.Atendimentos.Domain.Consulta;
using FluentAssertions;

namespace Advocacia.Modules.Atendimentos.Domain.UnitTests.Consulta;

/// <summary>As regras da verificação de identidade: TTL, 3 falhas, bloqueio e o que NÃO pode ser contornado.</summary>
public class ConsultaSessaoTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid();
    private static readonly AtendimentoId Atendimento = AtendimentoId.New();
    private static readonly DateTimeOffset T0 = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static ConsultaSessao NewSession(int inheritedFailures = 0, DateTimeOffset? at = null) =>
        ConsultaSessao.Start(Tenant, Atendimento, Client, inheritedFailures, at ?? T0);

    // ---- estado inicial e TTL ----

    [Fact]
    public void ANewSession_WaitsForTheDocument_AndExpiresInTenMinutes()
    {
        var sessao = NewSession();

        sessao.Etapa(T0).Should().Be(ConsultaEtapa.AguardandoDocumento);
        sessao.ExpiresAt.Should().Be(T0.AddMinutes(10));
        sessao.FailedAttempts.Should().Be(0);
        sessao.ProcessIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData(599, ConsultaEtapa.AguardandoDocumento)]
    [InlineData(600, ConsultaEtapa.Expirada)]
    [InlineData(601, ConsultaEtapa.Expirada)]
    public void TheTtlBoundary_IsExactlyTenMinutes(int seconds, ConsultaEtapa expected)
    {
        NewSession().Etapa(T0.AddSeconds(seconds)).Should().Be(expected);
    }

    [Fact]
    public void AnExpiredSession_DoesNotAcceptAnAttempt_EvenWithTheRightDocument()
    {
        var sessao = NewSession();
        var late = T0.AddMinutes(10);

        sessao.RegisterAttempt(documentMatches: true, late).Should().Be(ConsultaEtapa.Expirada);

        sessao.VerifiedAt.Should().BeNull();
        sessao.Etapa(late).Should().Be(ConsultaEtapa.Expirada);
    }

    [Fact]
    public void VerifyingDoesNotExtendTheTtl_TheAccessWindowEndsTenMinutesAfterTheStart()
    {
        var sessao = NewSession();

        sessao.RegisterAttempt(true, T0.AddMinutes(9)).Should().Be(ConsultaEtapa.Verificado);

        sessao.Etapa(T0.AddMinutes(9.9)).Should().Be(ConsultaEtapa.Verificado);
        sessao.Etapa(T0.AddMinutes(10)).Should().Be(ConsultaEtapa.Expirada);
    }

    // ---- sucesso e falha ----

    [Fact]
    public void TheRightDocument_Verifies()
    {
        var sessao = NewSession();

        sessao.RegisterAttempt(true, T0.AddMinutes(1)).Should().Be(ConsultaEtapa.Verificado);

        sessao.Etapa(T0.AddMinutes(2)).Should().Be(ConsultaEtapa.Verificado);
        sessao.VerifiedAt.Should().Be(T0.AddMinutes(1));
    }

    [Fact]
    public void AWrongDocument_IsDenied_AndTheSessionKeepsWaiting()
    {
        var sessao = NewSession();

        sessao.RegisterAttempt(false, T0.AddMinutes(1)).Should().Be(ConsultaEtapa.Negado);

        sessao.FailedAttempts.Should().Be(1);
        sessao.Etapa(T0.AddMinutes(1)).Should().Be(ConsultaEtapa.AguardandoDocumento, "Negado é o resultado de uma tentativa, não um estado guardado");
    }

    [Fact]
    public void TheDeniedThenRightSequence_Verifies_AndResetsTheCounter()
    {
        var sessao = NewSession();
        sessao.RegisterAttempt(false, T0.AddMinutes(1));
        sessao.RegisterAttempt(false, T0.AddMinutes(2));

        sessao.RegisterAttempt(true, T0.AddMinutes(3)).Should().Be(ConsultaEtapa.Verificado);

        sessao.FailedAttempts.Should().Be(0);
    }

    // ---- 3 falhas bloqueiam por 30 min ----

    [Fact]
    public void TheThirdFailure_Blocks_ForThirtyMinutes()
    {
        var sessao = NewSession();
        sessao.RegisterAttempt(false, T0.AddMinutes(1)).Should().Be(ConsultaEtapa.Negado);
        sessao.RegisterAttempt(false, T0.AddMinutes(2)).Should().Be(ConsultaEtapa.Negado);

        var third = T0.AddMinutes(3);
        sessao.RegisterAttempt(false, third).Should().Be(ConsultaEtapa.Bloqueado);

        sessao.BlockedUntil.Should().Be(third.AddMinutes(30));
        sessao.FailedAttempts.Should().Be(3);
    }

    [Fact]
    public void WhileBlocked_EvenTheRightDocumentIsRefused()
    {
        var sessao = NewSession();
        for (var i = 1; i <= 3; i++)
        {
            sessao.RegisterAttempt(false, T0.AddSeconds(i));
        }

        sessao.RegisterAttempt(true, T0.AddMinutes(1)).Should().Be(ConsultaEtapa.Bloqueado);

        sessao.VerifiedAt.Should().BeNull();
        sessao.FailedAttempts.Should().Be(3, "tentar enquanto bloqueado não conta nem verifica");
    }

    [Theory]
    [InlineData(29 * 60 + 59, ConsultaEtapa.Bloqueado)]
    public void TheBlockBoundary_IsThirtyMinutesFromTheThirdFailure(int secondsAfterBlock, ConsultaEtapa expected)
    {
        var sessao = NewSession();
        var blockedAt = T0.AddSeconds(3);
        sessao.RegisterAttempt(false, T0.AddSeconds(1));
        sessao.RegisterAttempt(false, T0.AddSeconds(2));
        sessao.RegisterAttempt(false, blockedAt);

        sessao.Etapa(blockedAt.AddSeconds(secondsAfterBlock)).Should().Be(expected);
        // Passados os 30 minutos a sessão (de 10 min) há muito expirou: o cliente recomeça com "iniciar".
        sessao.Etapa(blockedAt.AddMinutes(30)).Should().Be(ConsultaEtapa.Expirada);
    }

    [Fact]
    public void ABlockedSession_ReportsBlocked_NotExpired_EvenAfterTheTtl()
    {
        var sessao = NewSession();
        for (var i = 1; i <= 3; i++)
        {
            sessao.RegisterAttempt(false, T0.AddSeconds(i));
        }

        sessao.Etapa(T0.AddMinutes(15)).Should().Be(ConsultaEtapa.Bloqueado, "o bloqueio vale 30 min e prevalece sobre o TTL de 10");
    }

    // ---- falhas não se zeram recomeçando ----

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void AttemptsLeftBeforeTheBlock_DependOnTheFailuresInherited(int inherited, int attemptsUntilBlocked)
    {
        var sessao = NewSession(inherited);
        var result = ConsultaEtapa.Negado;
        var attempts = 0;

        while (result != ConsultaEtapa.Bloqueado)
        {
            result = sessao.RegisterAttempt(false, T0.AddSeconds(++attempts));
        }

        attempts.Should().Be(attemptsUntilBlocked);
    }

    [Fact]
    public void InheritedFailures_AreCappedBelowTheLimit_SoARestartAfterABlockIsNotBlockedAtOnce()
    {
        NewSession(inheritedFailures: 99).FailedAttempts.Should().Be(ConsultaSessao.MaxFailedAttempts - 1);
        NewSession(inheritedFailures: -5).FailedAttempts.Should().Be(0);
    }

    // ---- recomeçar ----

    [Fact]
    public void Invalidate_EndsTheSession_ButKeepsTheFailuresAndAnyBlock()
    {
        var sessao = NewSession();
        sessao.RegisterAttempt(false, T0.AddMinutes(1));

        sessao.Invalidate(T0.AddMinutes(2));

        sessao.Etapa(T0.AddMinutes(2)).Should().Be(ConsultaEtapa.Expirada);
        sessao.FailedAttempts.Should().Be(1);
    }

    [Fact]
    public void Invalidate_NeverExtendsALifetime()
    {
        var sessao = NewSession();

        sessao.Invalidate(T0.AddHours(5));

        sessao.ExpiresAt.Should().Be(T0.AddMinutes(10));
    }

    // ---- processos fixados ----

    [Fact]
    public void AttachedProcesses_AreDeduplicated_Ordered_AndCappedAtTen()
    {
        var sessao = NewSession();
        var ids = Enumerable.Range(0, 15).Select(_ => Guid.NewGuid()).ToList();

        sessao.AttachProcesses(ids.Concat([ids[0], ids[1]]));

        sessao.ProcessIds.Should().HaveCount(ConsultaSessao.MaxProcesses).And.Equal(ids.Take(10));
    }

    // ---- redação do histórico ----

    [Fact]
    public void WhileWaitingForTheDocument_TheClientsMessagesAreRedacted()
    {
        NewSession().ShouldRedactInbound(T0.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void RightAfterAnAttempt_TheMessageStillIsRedacted_WhateverTheOrderOfTheCalls()
    {
        // O Bot pode registrar a mensagem DEPOIS de verificar; já verificada, a etapa não é mais
        // "aguardando", mas o documento acabou de ser digitado.
        var sessao = NewSession();
        sessao.RegisterAttempt(true, T0.AddMinutes(1));

        sessao.ShouldRedactInbound(T0.AddMinutes(1).AddSeconds(59)).Should().BeTrue();
        sessao.ShouldRedactInbound(T0.AddMinutes(1).AddSeconds(60)).Should().BeFalse();
    }

    [Fact]
    public void AfterTheVerificationWindow_OrWhenExpired_NothingIsRedacted()
    {
        var sessao = NewSession();

        sessao.ShouldRedactInbound(T0.AddMinutes(11)).Should().BeFalse("uma conversa normal depois da sessão não é ocultada");
    }

    [Fact]
    public void TheSessionHasNoFieldThatCouldHoldTheDocument()
    {
        var members = typeof(ConsultaSessao).GetProperties().Select(p => p.Name.ToLowerInvariant());

        members.Should().NotContain(name => name.Contains("documento") || name.Contains("cpf") || name.Contains("cnpj") || name.Contains("document"));
    }
}

public class DocumentoConsultaTests
{
    [Theory]
    [InlineData("123.456.789-09", "12345678909")]
    [InlineData("123 456 789 09", "12345678909")]
    [InlineData("  12345678909  ", "12345678909")]
    [InlineData("CPF: 123.456.789-09", "12345678909")]
    [InlineData("12.345.678/0001-95", "12345678000195")]
    [InlineData("12345678000195", "12345678000195")]
    public void PunctuationAndTextAreStripped(string typed, string expected)
    {
        DocumentoConsulta.Normalize(typed).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem números")]
    public void NothingUsableNormalizesToEmpty(string? typed)
    {
        DocumentoConsulta.Normalize(typed).Should().BeEmpty();
    }

    [Theory]
    [InlineData("12345678909", true)]
    [InlineData("12345678000195", true)]
    [InlineData("1234567890", false)]
    [InlineData("123456789012", false)]
    [InlineData("", false)]
    public void OnlyCpfOrCnpjLengthsArePlausible(string digits, bool expected)
    {
        DocumentoConsulta.HasPlausibleLength(digits).Should().Be(expected);
    }

    [Fact]
    public void ArabicIndicAndOtherUnicodeDigitsAreNotAccepted()
    {
        // char.IsDigit aceitaria "١٢٣"; o documento é ASCII.
        DocumentoConsulta.Normalize("١٢٣٤٥٦٧٨٩٠١").Should().BeEmpty();
    }
}
