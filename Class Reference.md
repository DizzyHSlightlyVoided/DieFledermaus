# Type: `public class DieFledermaus.DieFledermausStream`
Provides methods and properties for compressing and decompressing files and streams in the DieFledermaus format, which is just the DEFLATE algorithm prefixed with magic number " `mAuS`" and metadata.

### Remarks
Unlike [`DeflateStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.deflatestream.aspx), this method reads part of the stream during the constructor, rather than the first call to [`DieFledermausStream.Read(System.Byte[],System.Int32,System.Int32)`](#method-diefledermausdiefledermausstreamreadsystembytesystemint32systemint32).

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, System.IO.Compression.CompressionMode compressionMode, System.Boolean leaveOpen)`
Creates a new instance with the specified mode.
* `stream`: The stream containing compressed data.
* `compressionMode`: Indicates whether the stream should be in compression or decompression mode.
* `leaveOpen`: `true` to leave open `stream` when the current instance is disposed; `false` to close `stream`.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionMode` is not a valid [`CompressionMode`](https://msdn.microsoft.com/en-us/library/system.io.compression.compressionmode.aspx) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`compressionMode` is [`CompressionMode.Compress`](https://msdn.microsoft.com/en-us/library/system.io.compression.compressionmode.compress.aspx), and `stream` does not support writing.

-OR-

`compressionMode` is [`CompressionMode.Decompress`](https://msdn.microsoft.com/en-us/library/system.io.compression.compressionmode.decompress.aspx), and `stream` does not support reading.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
The stream is in read-mode, and `stream` contains invalid data.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The stream is in read-mode, and `stream` contains data which is a lower version than the one expected.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, System.IO.Compression.CompressionMode compressionMode)`
Creates a new instance with the specified mode.
* `stream`: The stream containing compressed data.
* `compressionMode`: Indicates whether the stream should be in compression or decompression mode.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionMode` is not a valid [`CompressionMode`](https://msdn.microsoft.com/en-us/library/system.io.compression.compressionmode.aspx) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`compressionMode` is [`CompressionMode.Compress`](https://msdn.microsoft.com/en-us/library/system.io.compression.compressionmode.compress.aspx), and `stream` does not support writing.

-OR-

`compressionMode` is [`CompressionMode.Decompress`](https://msdn.microsoft.com/en-us/library/system.io.compression.compressionmode.decompress.aspx), and `stream` does not support reading.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, DieFledermaus.MausCompressionFormat compressionFormat, DieFledermaus.MausEncryptionFormat encryptionFormat, System.Boolean leaveOpen)`
Creates a new instance in write-mode, with the specified compression and encryption formats.
* `stream`: The stream containing compressed data.
* `compressionFormat`: Indicates the format of the stream.
* `encryptionFormat`: Indicates the encryption format.
* `leaveOpen`: `true` to leave open `stream` when the current instance is disposed; `false` to close `stream`.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionFormat` is not a valid [`MausCompressionFormat`](#type-public-enum-diefledermausmauscompressionformat) value.

-OR-

`encryptionFormat` is not a valid [`MausEncryptionFormat`](#type-public-enum-diefledermausmausencryptionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, DieFledermaus.MausCompressionFormat compressionFormat, DieFledermaus.MausEncryptionFormat encryptionFormat)`
Creates a new instance in write-mode, with the specified compression and encryption formats.
* `stream`: The stream containing compressed data.
* `compressionFormat`: Indicates the format of the stream.
* `encryptionFormat`: Indicates the encryption format.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionFormat` is not a valid [`MausCompressionFormat`](#type-public-enum-diefledermausmauscompressionformat) value.

-OR-

`encryptionFormat` is not a valid [`MausEncryptionFormat`](#type-public-enum-diefledermausmausencryptionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, DieFledermaus.MausCompressionFormat compressionFormat, System.Boolean leaveOpen)`
Creates a new instance in write-mode, with the specified compression and no encryption.
* `stream`: The stream containing compressed data.
* `compressionFormat`: Indicates the format of the stream.
* `leaveOpen`: `true` to leave open `stream` when the current instance is disposed; `false` to close `stream`.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionFormat` is not a valid [`MausCompressionFormat`](#type-public-enum-diefledermausmauscompressionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, DieFledermaus.MausCompressionFormat compressionFormat)`
Creates a new instance in write-mode, with the specified compression format and no encryption.
* `stream`: The stream containing compressed data.
* `compressionFormat`: Indicates the format of the stream.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionFormat` is not a valid [`MausCompressionFormat`](#type-public-enum-diefledermausmauscompressionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, DieFledermaus.MausEncryptionFormat encryptionFormat, System.Boolean leaveOpen)`
Creates a new instance in write-mode with the specified encryption format.
* `stream`: The stream containing compressed data.
* `encryptionFormat`: Indicates the encryption format.
* `leaveOpen`: `true` to leave open `stream` when the current instance is disposed; `false` to close `stream`.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`encryptionFormat` is not a valid [`MausEncryptionFormat`](#type-public-enum-diefledermausmausencryptionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, DieFledermaus.MausEncryptionFormat encryptionFormat)`
Creates a new instance in write-mode with the specified encryption format.
* `stream`: The stream containing compressed data.
* `encryptionFormat`: Indicates the encryption format.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`encryptionFormat` is not a valid [`MausEncryptionFormat`](#type-public-enum-diefledermausmausencryptionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, System.IO.Compression.CompressionLevel compressionLevel, System.Boolean leaveOpen)`
Creates a new instance in write-mode using DEFLATE with the specified compression level.
* `stream`: The stream to which compressed data will be written.
* `compressionLevel`: Indicates the compression level of the stream.
* `leaveOpen`: `true` to leave open `stream` when the current instance is disposed; `false` to close `stream`.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionLevel` is not a valid [`CompressionLevel`](https://msdn.microsoft.com/en-us/library/system.io.compression.compressionlevel.aspx) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, System.IO.Compression.CompressionLevel compressionLevel)`
Creates a new instance in write-mode using DEFLATE with the specified compression level.
* `stream`: The stream to which compressed data will be written.
* `compressionLevel`: Indicates the compression level of the stream.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionLevel` is not a valid [`CompressionLevel`](https://msdn.microsoft.com/en-us/library/system.io.compression.compressionlevel.aspx) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, System.IO.Compression.CompressionLevel compressionLevel, DieFledermaus.MausEncryptionFormat encryptionFormat, System.Boolean leaveOpen)`
Creates a new instance in write-mode using DEFLATE with the specified compression level and encryption format.
* `stream`: The stream to which compressed data will be written.
* `compressionLevel`: Indicates the compression level of the stream.
* `encryptionFormat`: Indicates the encryption format.
* `leaveOpen`: `true` to leave open `stream` when the current instance is disposed; `false` to close `stream`.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionLevel` is not a valid [`CompressionLevel`](https://msdn.microsoft.com/en-us/library/system.io.compression.compressionlevel.aspx) value.

-OR-

`encryptionFormat` is not a valid [`MausEncryptionFormat`](#type-public-enum-diefledermausmausencryptionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, System.IO.Compression.CompressionLevel compressionLevel, DieFledermaus.MausEncryptionFormat encryptionFormat)`
Creates a new instance in write-mode using DEFLATE with the specified compression level and encryption format.
* `stream`: The stream to which compressed data will be written.
* `compressionLevel`: Indicates the compression level of the stream.
* `encryptionFormat`: Indicates the encryption format.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionLevel` is not a valid [`CompressionLevel`](https://msdn.microsoft.com/en-us/library/system.io.compression.compressionlevel.aspx) value.

-OR-

`encryptionFormat` is not a valid [`MausEncryptionFormat`](#type-public-enum-diefledermausmausencryptionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Method: `DieFledermausStream.GetKeySizes(DieFledermaus.MausEncryptionFormat encryptionFormat, out System.Int32 blockBitCount)`
Gets a [`KeySizes`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.keysizes.aspx) value indicating the valid key sizes for the specified encryption scheme.
* `encryptionFormat`: The encryption format to check.
* `blockBitCount`: When this method returns, contains the number of bits in a single block of encrypted data, or `none` if `encryptionFormat` is [`MausEncryptionFormat.None`](#mausencryptionformatnone--0). This parameter is passed uninitialized.

**Returns:** A [`KeySizes`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.keysizes.aspx) value indicating the valid key sizes for `encryptionFormat`, or `null` if `encryptionFormat` is [`MausEncryptionFormat.None`](#mausencryptionformatnone--0)

--------------------------------------------------

## Method: `public static System.Security.Cryptography.KeySizes GetKeySizes(DieFledermaus.MausEncryptionFormat encryptionFormat)`
Gets a [`KeySizes`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.keysizes.aspx) value indicating the valid key sizes for the specified encryption scheme.
* `encryptionFormat`: The encryption format to check.

**Returns:**  Type [`KeySizes`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.keysizes.aspx): A [`KeySizes`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.keysizes.aspx) value indicating the valid key sizes for `encryptionFormat`, or `null` if `encryptionFormat` is [`MausEncryptionFormat.None`](#mausencryptionformatnone--0)

--------------------------------------------------

## Property: `override System.Boolean CanRead { get; }`
Gets a value indicating whether the current stream supports reading.

--------------------------------------------------

## Property: `override System.Boolean CanSeek { get; }`
Gets a value indicating whether the current stream supports reading. Always returns `false`.

--------------------------------------------------

## Property: `override System.Boolean CanWrite { get; }`
Gets a value indicating whether the current stream supports writing.

--------------------------------------------------

## Property: `DieFledermaus.MausEncryptionFormat EncryptionFormat { get; }`
Gets the encryption format of the current instance.

--------------------------------------------------

## Property: `DieFledermaus.MausCompressionFormat CompressionFormat { get; }`
Gets the compression format of the current instance.

--------------------------------------------------

## Property: `System.Nullable<T> CreatedTime { get; set; }`
Gets and sets the time at which the underlying file was created, or `null` to specify no creation time.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current stream is closed.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
In a set operation, the current stream is in read-mode.

--------------------------------------------------

## Property: `System.Nullable<T> ModifiedTime { get; set; }`
Gets and sets the time at which the underlying file was last modified prior to being archived, or `null` to specify no modification time.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current stream is closed.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
In a set operation, the current stream is in read-mode.

--------------------------------------------------

## Property: `System.Byte[] Key { get; set; }`
Gets and sets the key used to encrypt the DieFledermaus stream.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current stream is closed.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
In a set operation, the current stream is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
In a set operation, the specified value is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the specified value is an invalid length according to [`DieFledermausStream.KeySizes`](#property-override-systemsecuritycryptographykeysizes-keysizes--get-).

--------------------------------------------------

## Method: `public System.Boolean IsValidKeyByteSize(System.Int32 byteCount)`
Determines whether the specified value is a valid length for [`DieFledermausStream.Key`](#property-systembyte-key--get-set-), in bytes.
* `byteCount`: The number of bytes to test.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `byteCount` is a valid byte count according to [`DieFledermausStream.KeySizes`](#property-override-systemsecuritycryptographykeysizes-keysizes--get-); `false` if `byteCount` is invalid, or if the current instance is not encrypted.

--------------------------------------------------

## Method: `public System.Boolean IsValidKeyBitSize(System.Int32 bitCount)`
Determines whether the specified value is a valid length for [`DieFledermausStream.Key`](#property-systembyte-key--get-set-), in bits.
* `bitCount`: The number of bits to test.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `bitCount` is a valid bit count according to [`DieFledermausStream.KeySizes`](#property-override-systemsecuritycryptographykeysizes-keysizes--get-); `false` if `bitCount` is invalid, or if the current instance is not encrypted.

--------------------------------------------------

## Method: `public static System.Boolean IsValidKeyByteSize(System.Int32 byteCount, DieFledermaus.MausEncryptionFormat encryptionFormat)`
Determines whether the specified value is a valid length for [`DieFledermausStream.Key`](#property-systembyte-key--get-set-), in bytes.
* `byteCount`: The number of bytes to test.
* `encryptionFormat`: The encryption format to test for.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `byteCount` is a valid byte count according to `encryptionFormat`; `false` if `byteCount` is invalid, or if the current instance is not encrypted.

### Exceptions
##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`encryptionFormat` is not a valid [`MausEncryptionFormat`](#type-public-enum-diefledermausmausencryptionformat) value.

--------------------------------------------------

## Method: `public static System.Boolean IsValidKeyBitSize(System.Int32 bitCount, DieFledermaus.MausEncryptionFormat encryptionFormat)`
Determines whether the specified value is a valid length for [`DieFledermausStream.Key`](#property-systembyte-key--get-set-), in bits.
* `bitCount`: The number of bits to test.
* `encryptionFormat`: The encryption format to test for.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `bitCount` is a valid bit count according to [`DieFledermausStream.KeySizes`](#property-override-systemsecuritycryptographykeysizes-keysizes--get-); `false` if `bitCount` is invalid, or if the current instance is not encrypted.

### Exceptions
##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`encryptionFormat` is not a valid [`MausEncryptionFormat`](#type-public-enum-diefledermausmausencryptionformat) value.

--------------------------------------------------

## Property: `System.Int32 BlockSize { get; }`
Gets the number of bits in a single block of encrypted data, or 0 if the current instance is not encrypted.

--------------------------------------------------

## Property: `System.Int32 BlockByteCount { get; }`
Gets the number of bytes in a single block of encrypted data, or 0 if the current instance is not encrypted.

--------------------------------------------------

## Property: `System.String Comment { get; set; }`
Gets and sets a comment on the file.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current instance is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current instance is in read-mode.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the specified value is not `null`, and has a length of either 0 or which is greater than 65536.

--------------------------------------------------

## Property: `DieFledermaus.DieFledermausStream.SettableOptions EncryptedOptions { get; }`
Gets a collection containing options which should be encrypted, or `null` if the current instance is not encrypted.

--------------------------------------------------

## Property: `System.String Filename { get; set; }`
Gets and sets a filename for the current instance.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current instance is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current instance is in read-mode.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the specified value is not `null` and is invalid.
### See Also
* [`DieFledermausStream.IsValidFilename(System.String)`](#method-public-static-systemboolean-isvalidfilenamesystemstring-value)

--------------------------------------------------

## Method: `public static System.Boolean IsValidFilename(System.String value)`
Determines if the specified value is a valid value for the [`DieFledermausStream.Filename`](#property-systemstring-filename--get-set-) property.
* `value`: The value to set.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `value` is a valid filename; `false` if `value` has a length of 0, has a length greater than 256 UTF-8 characters, contains unpaired surrogate characters, contains non-whitespace control characters (non-whitespace characters between `\u0000` and `\u001f` inclusive, or between `\u007f` and `\u009f` inclusive), contains only whitespace, or is "." or ".." (the "current directory" and "parent directory" identifiers).

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`value` is `null`.

--------------------------------------------------

## Method: `public void SetPassword(System.String password)`
Sets [`DieFledermausStream.Key`](#property-systembyte-key--get-set-) to a value derived from the specified password.
* `password`: The password to set.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is closed.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current stream is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public void SetPassword(System.Security.SecureString password)`
Sets [`DieFledermausStream.Key`](#property-systembyte-key--get-set-) to a value derived from the specified password.
* `password`: The password to set.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is closed.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current stream is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public override void Flush()`
Flushes the contents of the internal buffer of the current stream object to the underlying stream.

--------------------------------------------------

## Property: `override System.Int64 Length { get; }`
Gets the length of the stream. This property is not supported and always throws [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx).

### Exceptions
##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
Always.

--------------------------------------------------

## Property: `override System.Int64 Position { get; set; }`
Gets and sets the position in the stream. This property is not supported and always throws [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx).

### Exceptions
##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
Always.

--------------------------------------------------

## Method: `public override void SetLength(System.Int64 value)`
Sets the length of the stream. This method is not supported and always throws [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx).
* `value`: This parameter is ignored.

### Exceptions
##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
Always.

--------------------------------------------------

## Method: `public override System.Int64 Seek(System.Int64 offset, System.IO.SeekOrigin origin)`
Seeks within the stream. This method is not supported and always throws [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx).
* `offset`: This parameter is ignored.
* `origin`: This parameter is ignored.

### Exceptions
##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
Always.

--------------------------------------------------

## Method: `public void LoadData()`
Attempts to pre-load the data in the current instance, and test whether [`DieFledermausStream.Key`](#property-systembyte-key--get-set-) is set to the correct value if the current stream is encrypted and to decrypt any encrypted options.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current stream is in write-mode.

##### [`CryptographicException`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.cryptographicexception.aspx)
[`DieFledermausStream.Key`](#property-systembyte-key--get-set-) is not set to the correct value. It is safe to attempt to call [`DieFledermausStream.LoadData()`](#method-public-void-loaddata) or [`DieFledermausStream.Read(System.Byte[],System.Int32,System.Int32)`](#method-diefledermausdiefledermausstreamreadsystembytesystemint32systemint32) again if this exception is caught.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
The stream contains invalid data.

##### [`IOException`](https://msdn.microsoft.com/en-us/library/system.io.ioexception.aspx)
An I/O error occurred.

--------------------------------------------------

## Method: `public override System.Int32 Read(System.Byte[] buffer, System.Int32 offset, System.Int32 count)`
Reads from the stream into the specified array.
* `buffer`: The array containing the bytes to write.
* `offset`: The index in `buffer` at which copying begins.
* `count`: The maximum number of bytes to read.

**Returns:**  Type [`Int32`](https://msdn.microsoft.com/en-us/library/system.int32.aspx): The number of bytes which were read.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current stream is in write-mode.

##### [`CryptographicException`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.cryptographicexception.aspx)
[`DieFledermausStream.Key`](#property-systembyte-key--get-set-) is not set to the correct value. It is safe to attempt to call [`DieFledermausStream.LoadData()`](#method-public-void-loaddata) or [`DieFledermausStream.Read(System.Byte[],System.Int32,System.Int32)`](#method-diefledermausdiefledermausstreamreadsystembytesystemint32systemint32) again if this exception is caught.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`buffer` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`offset` or `count` is less than 0.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`offset` plus `count` is greater than the length of `buffer`.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
The stream contains invalid data.

##### [`IOException`](https://msdn.microsoft.com/en-us/library/system.io.ioexception.aspx)
An I/O error occurred.

--------------------------------------------------

## Method: `public override System.Int32 ReadByte()`
Reads a single byte from the stream.

**Returns:**  Type [`Int32`](https://msdn.microsoft.com/en-us/library/system.int32.aspx): The unsigned byte cast to [`Int32`](https://msdn.microsoft.com/en-us/library/system.int32.aspx), or -1 if the current instance has reached the end of the stream.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current stream is in write-mode.

##### [`CryptographicException`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.cryptographicexception.aspx)
[`DieFledermausStream.Key`](#property-systembyte-key--get-set-) is not set to the correct value. It is safe to attempt to call [`DieFledermausStream.LoadData()`](#method-public-void-loaddata) or [`DieFledermausStream.Read(System.Byte[],System.Int32,System.Int32)`](#method-diefledermausdiefledermausstreamreadsystembytesystemint32systemint32) again if this exception is caught.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
The stream contains invalid data.

##### [`IOException`](https://msdn.microsoft.com/en-us/library/system.io.ioexception.aspx)
An I/O error occurred.

--------------------------------------------------

## Method: `public override void Write(System.Byte[] buffer, System.Int32 offset, System.Int32 count)`
Writes the specified byte array into the stream.
* `buffer`: The array containing the bytes to write.
* `offset`: The index in `buffer` at which writing begins.
* `count`: The number of bytes to write.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current stream is in read-mode.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`buffer` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`offset` or `count` is less than 0.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`offset` plus `count` is greater than the length of `buffer`.

##### [`IOException`](https://msdn.microsoft.com/en-us/library/system.io.ioexception.aspx)
An I/O error occurred.

--------------------------------------------------

## Method: `protected override void Dispose(System.Boolean disposing)`
Releases all unmanaged resources used by the current instance, and optionally releases all managed resources.
* `disposing`: `true` to release both managed and unmanaged resources; `false` to release only unmanaged resources.

--------------------------------------------------

# Type: `public sealed class DieFledermaus.DieFledermausStream.SettableOptions`
Represents a collection of [`MausOptionToEncrypt`](#type-public-enum-diefledermausmausoptiontoencrypt) options.

--------------------------------------------------

## Property: `virtual System.Int32 Count { get; }`
Gets the number of elements contained in the collection.

--------------------------------------------------

## Property: `virtual System.Boolean IsReadOnly { get; }`
Gets a value indicating whether the current instance is read-only. Returns `true` if the underlying stream is closed or is in read-mode; `false` otherwise.

### Remarks
This property indicates that the collection cannot be changed externally. If [`SettableOptions.IsFrozen`](#property-diefledermausdiefledermausstreamsettableoptionsisfrozen) is `false`, however,

--------------------------------------------------

## Method: `public System.Boolean Add(DieFledermaus.MausOptionToEncrypt option)`
Adds the specified value to the collection.
* `option`: The option to add.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `option` was successfully added; `false` if `option` already exists in the collection, or is not a valid [`MausOptionToEncrypt`](#type-public-enum-diefledermausmausoptiontoencrypt) value.

### Exceptions
##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`SettableOptions.IsReadOnly`](#property-virtual-systemboolean-isreadonly--get-) is `true`.

--------------------------------------------------

## Method: `public System.Boolean Remove(DieFledermaus.MausOptionToEncrypt option)`
Removes the specified value from the collection.
* `option`: The option to remove.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `option` was found and successfully removed; `false` otherwise.

### Exceptions
##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`SettableOptions.IsReadOnly`](#property-virtual-systemboolean-isreadonly--get-) is `true`.

--------------------------------------------------

## Method: `public void AddRange(System.Collections.Generic.IEnumerable<DieFledermaus.MausOptionToEncrypt> other)`
Adds all elements in the specified collection to the current instance (excluding duplicates and values already in the current collection).
* `other`: A collection containing other values to add.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`other` is `null`.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`SettableOptions.IsReadOnly`](#property-virtual-systemboolean-isreadonly--get-) is `true`.

--------------------------------------------------

## Method: `public void AddAll()`
Adds all available values to the collection.

### Exceptions
##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`SettableOptions.IsReadOnly`](#property-virtual-systemboolean-isreadonly--get-) is `true`.

--------------------------------------------------

## Method: `public void RemoveWhere(System.Predicate<DieFledermaus.MausOptionToEncrypt> match)`
Removes all elements matching the specified predicate from the list.
* `match`: A predicate defining the elements to remove.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`match` is `null`.

--------------------------------------------------

## Method: `public void Clear()`
Removes all elements from the collection.

--------------------------------------------------

## Method: `public System.Boolean Contains(DieFledermaus.MausOptionToEncrypt option)`
Determines if the specified value exists in the collection.
* `option`: The option to search for in the collection.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `option` was found; `false` otherwise.

--------------------------------------------------

## Method: `public void CopyTo(DieFledermaus.MausOptionToEncrypt[] array, System.Int32 arrayIndex)`
Copies all elements in the collection to the specified array, starting at the specified index.
* `array`: The array to which the collection will be copied. The array must have zero-based indexing.
* `arrayIndex`: The index in `array` at which copying begins.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`array` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`arrayIndex` is less than 0.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`arrayIndex` plus [`SettableOptions.Count`](#property-virtual-systemint32-count--get-) is greater than the length of `array`.

--------------------------------------------------

## Method: `public DieFledermaus.DieFledermausStream.SettableOptions.Enumerator GetEnumerator()`
Returns an enumerator which iterates through the collection.

**Returns:**  Type [`Enumerator`](#type-public-struct-diefledermausdiefledermausstreamsettableoptionsenumerator): An enumerator which iterates through the collection.

--------------------------------------------------

# Type: `public struct DieFledermaus.DieFledermausStream.SettableOptions.Enumerator`
An enumerator which iterates through the collection.

--------------------------------------------------

## Property: `DieFledermaus.MausOptionToEncrypt Current { get; }`
Gets the element at the current position in the enumerator.

--------------------------------------------------

## Method: `public void Dispose()`
Disposes of the current instance.

--------------------------------------------------

## Method: `public System.Boolean MoveNext()`
Advances the enumerator to the next position in the collection.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if the enumerator was successfully advanced; `false` if the enumerator has passed the end of the collection.

--------------------------------------------------

## Property: `override System.Security.Cryptography.KeySizes KeySizes { get; }`
Gets a [`KeySizes`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.keysizes.aspx) object indicating all valid key sizes.

--------------------------------------------------

# Type: `public enum DieFledermaus.MausEncryptionFormat`
Options indicating the format used to encrypt the DieFledermaus stream.

--------------------------------------------------

## `MausEncryptionFormat.None = 0`
The DieFledermaus stream is not encrypted.

--------------------------------------------------

## `MausEncryptionFormat.Aes = 1`
The DieFledermaus stream is encrypted using the Advanced Encryption Standard algorithm.

--------------------------------------------------

# Type: `public enum DieFledermaus.MausCompressionFormat`
Options indicating the format used to compress the DieFledermaus stream.

--------------------------------------------------

## `MausCompressionFormat.Deflate = 0`
The file is DEFLATE-compressed.

--------------------------------------------------

## `MausCompressionFormat.None = 1`
The file is not compressed.

--------------------------------------------------

## `MausCompressionFormat.Lzma = 2`
The file is compressed using the Lempel-Ziv-Markov chain algorithm

--------------------------------------------------

# Type: `public enum DieFledermaus.MausOptionToEncrypt`
Indicates values to encrypt.

--------------------------------------------------

## `MausOptionToEncrypt.Filename = 0`
Indicates that [`DieFledermausStream.Filename`](#property-systemstring-filename--get-set-) will be encrypted.

--------------------------------------------------

## `MausOptionToEncrypt.Compression = 1`
Indicates that [`DieFledermausStream.CompressionFormat`](#property-diefledermausmauscompressionformat-compressionformat--get-) will be encrypted.

--------------------------------------------------

## `MausOptionToEncrypt.ModTime = 2`
Indicates that [`DieFledermausStream.CreatedTime`](#property-systemnullablet-createdtime--get-set-) and [`DieFledermausStream.ModifiedTime`](#property-systemnullablet-modifiedtime--get-set-) will be encrypted.

--------------------------------------------------

## `MausOptionToEncrypt.Comment = 3`
Indicates that [`DieFledermausStream.Comment`](#property-systemstring-comment--get-set-) will be encrypted.
