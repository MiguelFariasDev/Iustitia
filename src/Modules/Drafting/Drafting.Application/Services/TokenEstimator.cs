namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>Estimativa de tokens por comprimento do texto (o tokenizador real não está disponível aqui).</summary>
public static class TokenEstimator
{
    public static int Estimate(string text, double charsPorToken)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return (int)Math.Ceiling(text.Length / Math.Max(1.0, charsPorToken));
    }
}
