using System.Runtime.CompilerServices;
using Advocacia.BuildingBlocks.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

/// <summary>Os conversores de colunas cifradas usam o acessor estático; chaves fixas só deste projeto de teste.</summary>
internal static class ModuleInit
{
    public static readonly FieldEncryptionOptions Chaves = new()
    {
        ActiveKeyId = "t1",
        Keys = new() { ["t1"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()) },
        BlindIndexKey = Convert.ToBase64String(Enumerable.Range(33, 32).Select(i => (byte)i).ToArray()),
    };

    [ModuleInitializer]
    internal static void Init() => FieldEncryption.Configure(new AesGcmFieldEncryptor(Options.Create(Chaves)));
}
