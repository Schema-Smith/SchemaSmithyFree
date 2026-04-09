// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace SchemaQuench.IntegrationTests.SqlServer;

/// <summary>
/// In-memory key store provider for Always Encrypted integration tests.
/// Generates RSA keys on construction — no external key store dependency.
/// </summary>
public class InMemoryKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
{
    public const string ProviderName = "IN_MEMORY_PROVIDER";
    public const string KeyPath = "InMemoryKey";

    private readonly RSA _rsa;

    public InMemoryKeyStoreProvider()
    {
        _rsa = RSA.Create(2048);
    }

    public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
    {
        return _rsa.Decrypt(encryptedColumnEncryptionKey, RSAEncryptionPadding.OaepSHA256);
    }

    public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
    {
        return _rsa.Encrypt(columnEncryptionKey, RSAEncryptionPadding.OaepSHA256);
    }

    /// <summary>
    /// Generates a random CEK (32 bytes for AES-256), encrypts it with the RSA key,
    /// and returns the hex string for use in CREATE COLUMN ENCRYPTION KEY DDL.
    /// </summary>
    public (byte[] PlainCek, string EncryptedHex) GenerateCekForDdl()
    {
        var cekBytes = RandomNumberGenerator.GetBytes(32);
        var encryptedCek = EncryptColumnEncryptionKey(KeyPath, "RSA_OAEP", cekBytes);
        var hex = "0x" + BitConverter.ToString(encryptedCek).Replace("-", "");
        return (cekBytes, hex);
    }
}
