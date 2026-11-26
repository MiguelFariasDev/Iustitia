using System.Runtime.CompilerServices;
using Advocacia.BuildingBlocks.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.IntegrationTests;

/// <summary>Os conversores de colunas cifradas usam o acessor estático; chaves fixas só deste projeto de teste.</summary>
internal static class ModuleInit
{
    [ModuleInitializer]
    internal static void Init() =>
        FieldEncryption.Configure(new AesGcmFieldEncryptor(Options.Create(new FieldEncryptionOptions
        {
            ActiveKeyId = "t1",
            Keys = new() { ["t1"] = Convert.ToBase64String(new byte[32]) },
            BlindIndexKey = Convert.ToBase64String(new byte[32]),
        })));
}
