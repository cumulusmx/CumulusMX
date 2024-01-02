using System;
using System.IO;
using System.Security.Cryptography;

namespace CumulusMX
{
	/*
		MIT License

		Copyright (c) 2019 Kashif Jamal Soofi

		Permission is hereby granted, free of charge, to any person obtaining a copy
		of this software and associated documentation files (the "Software"), to deal
		in the Software without restriction, including without limitation the rights
		to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
		copies of the Software, and to permit persons to whom the Software is
		furnished to do so, subject to the following conditions:

		The above copyright notice and this permission notice shall be included in all
		copies or substantial portions of the Software.

		THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
		IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
		FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
		AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
		LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
		OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
		SOFTWARE.

		https://github.com/kashifsoofi/crypto-sandbox/blob/master/dotnet/src/Sandbox.Crypto/AesCrypto.cs
	*/

	public static class Crypto
	{
		public static byte[] GenerateKey()
		{
			var key = new byte[256 / 8]; // use 256 bits
			var rnd = new Random();
			rnd.NextBytes(key);
			return key;
		}


		public static string EncryptString(string plainText, byte[] key, string description)
		{
			try
			{
				if (string.IsNullOrEmpty(plainText))
					return string.Empty;

				using var aes = Aes.Create();
				aes.Key = key;
				var cryptoTransform = aes.CreateEncryptor(aes.Key, aes.IV);
				var cipherText = Encrypt(plainText, cryptoTransform);
				var data = new byte[cipherText.Length + aes.IV.Length + 1];
				data[0] = (byte)aes.IV.Length;
				Array.Copy(aes.IV, 0, data, 1, aes.IV.Length);
				Array.Copy(cipherText, 0, data, aes.IV.Length + 1, cipherText.Length);
				return Convert.ToBase64String(data);
			}
			catch (Exception e)
			{
				Program.cumulus.LogMessage($"Data was not encrypted. An error occurred processing '{description}'");
				Program.cumulus.LogMessage(e.ToString());
				return null;
			}
		}

		public static string DecryptString(string encryptedText, byte[] key, string description)
		{
			try
			{
				if (encryptedText.Length == 0)
					return string.Empty;

				var data = Convert.FromBase64String(encryptedText);
				byte ivSize = data[0];
				var iv = new byte[ivSize];
				Array.Copy(data, 1, iv, 0, ivSize);
				var encrypted = new byte[data.Length - ivSize - 1];
				Array.Copy(data, ivSize + 1, encrypted, 0, encrypted.Length);

				using var aes = Aes.Create();
				aes.Key = key;
				aes.IV = iv;

				var cryptoTransform = aes.CreateDecryptor(aes.Key, aes.IV);
				return Decrypt(encrypted, cryptoTransform);
			}
			catch (Exception e)
			{
				Program.cumulus.LogMessage($"Data was not decrypted. An error occurred processing '{description}'");
				Program.cumulus.LogMessage(e.ToString());
				return null;
			}
		}

		private static byte[] Encrypt(string data, ICryptoTransform cryptoTransform)
		{
			if (data == null || data.Length <= 0)
				throw new ArgumentException("Invalid data", nameof(data));

			using var memoryStream = new MemoryStream();
			using (var cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write))
			{
				using var writer = new StreamWriter(cryptoStream);
				writer.Write(data);
			}

			return memoryStream.ToArray();
		}

		private static string Decrypt(byte[] data, ICryptoTransform cryptoTransform)
		{
			if (data == null || data.Length <= 0)
				throw new ArgumentException("Invalid data", nameof(data));

			using var memoryStream = new MemoryStream(data);
			using var cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Read);
			using var reader = new StreamReader(cryptoStream);

			return reader.ReadToEnd();
		}
	}
}
