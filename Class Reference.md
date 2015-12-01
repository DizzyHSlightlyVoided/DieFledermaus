# Type: `public class DieFledermaus.DieFledermausStream`
Provides methods and properties for compressing and decompressing files and streams in the DieFledermaus format, which is just the DEFLATE algorithm prefixed with magic number " `mAuS`" and metadata.

### Remarks
Unlike streams such as [`DeflateStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.deflatestream.aspx), this method reads part of the stream during the constructor, rather than the first call to [`DieFledermausStream.Read(System.Byte[],System.Int32,System.Int32)`](#method-diefledermausdiefledermausstreamreadsystembytesystemint32systemint32).

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, System.IO.Compression.CompressionMode compressionMode, System.Boolean leaveOpen)`
Creates a new instance with the specified mode.
* `stream`: The stream containing compressed data.
* `compressionMode`: Indicates whether the stream should be in compression or decompression mode.
* `leaveOpen`: `true` to leave `stream` open when the current instance is disposed; `false` to close `stream`.

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
* `leaveOpen`: `true` to leave `stream` open when the current instance is disposed; `false` to close `stream`.

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
* `leaveOpen`: `true` to leave `stream` open when the current instance is disposed; `false` to close `stream`.

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
* `leaveOpen`: `true` to leave `stream` open when the current instance is disposed; `false` to close `stream`.

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

## Constructor: `public DieFledermausStream(System.IO.Stream stream, DieFledermaus.LzmaDictionarySize dictionarySize, System.Boolean leaveOpen)`
Creates a new instance in write-mode with LZMA encryption, using the specified dictionary size and no encryption.
* `stream`: The stream containing compressed data.
* `dictionarySize`: Indicates the size of the dictionary, in bytes.
* `leaveOpen`: `true` to leave `stream` open when the current instance is disposed; `false` to close `stream`.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`dictionarySize` is an integer value less than [`LzmaDictionarySize.MinValue`](#lzmadictionarysizeminvalue--16384) or greater than [`LzmaDictionarySize.MaxValue`](#lzmadictionarysizemaxvalue--67108864).

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, DieFledermaus.LzmaDictionarySize dictionarySize)`
Creates a new instance in write-mode with LZMA encryption, using the specified dictionary size and no encryption.
* `stream`: The stream containing compressed data.
* `dictionarySize`: Indicates the size of the dictionary, in bytes.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`dictionarySize` is an integer value less than [`LzmaDictionarySize.MinValue`](#lzmadictionarysizeminvalue--16384) or greater than [`LzmaDictionarySize.MaxValue`](#lzmadictionarysizemaxvalue--67108864).

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, DieFledermaus.LzmaDictionarySize dictionarySize, DieFledermaus.MausEncryptionFormat encryptionFormat, System.Boolean leaveOpen)`
Creates a new instance in write-mode with LZMA encryption, using the specified dictionary size and no encryption.
* `stream`: The stream containing compressed data.
* `dictionarySize`: Indicates the size of the dictionary, in bytes.
* `encryptionFormat`: Indicates the encryption format.
* `leaveOpen`: `true` to leave `stream` open when the current instance is disposed; `false` to close `stream`.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`dictionarySize` is an integer value less than [`LzmaDictionarySize.MinValue`](#lzmadictionarysizeminvalue--16384) or greater than [`LzmaDictionarySize.MaxValue`](#lzmadictionarysizemaxvalue--67108864).

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, DieFledermaus.LzmaDictionarySize dictionarySize, DieFledermaus.MausEncryptionFormat encryptionFormat)`
Creates a new instance in write-mode with LZMA encryption, using the specified dictionary size and no encryption.
* `stream`: The stream containing compressed data.
* `dictionarySize`: Indicates the size of the dictionary, in bytes.
* `encryptionFormat`: Indicates the encryption format.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`dictionarySize` is an integer value less than [`LzmaDictionarySize.MinValue`](#lzmadictionarysizeminvalue--16384) or greater than [`LzmaDictionarySize.MaxValue`](#lzmadictionarysizemaxvalue--67108864).

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

--------------------------------------------------

## Constructor: `public DieFledermausStream(System.IO.Stream stream, System.IO.Compression.CompressionLevel compressionLevel, System.Boolean leaveOpen)`
Creates a new instance in write-mode using DEFLATE with the specified compression level.
* `stream`: The stream to which compressed data will be written.
* `compressionLevel`: Indicates the compression level of the stream.
* `leaveOpen`: `true` to leave `stream` open when the current instance is disposed; `false` to close `stream`.

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
* `leaveOpen`: `true` to leave `stream` open when the current instance is disposed; `false` to close `stream`.

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

## Property: `System.Security.Cryptography.KeySizes KeySizes { get; }`
Gets a [`KeySizes`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.keysizes.aspx) object indicating all valid key sizes for the current encryption, or `null` if the current stream is not encrypted.

--------------------------------------------------

## Property: `DieFledermaus.MausCompressionFormat CompressionFormat { get; }`
Gets the compression format of the current instance.

--------------------------------------------------

## Property: `System.Nullable<T> CreatedTime { get; set; }`
Gets and sets the time at which the underlying file was created, or `null` to specify no creation time.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current stream is in read-mode.

--------------------------------------------------

## Property: `System.Nullable<T> ModifiedTime { get; set; }`
Gets and sets the time at which the underlying file was last modified prior to being archived, or `null` to specify no modification time.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current stream is in read-mode.

--------------------------------------------------

## Property: `System.Byte[] IV { get; set; }`
Gets and sets the initialization vector used when encrypting the current instance.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current stream is in write-mode.

-OR-

In a set operation, the current stream is not encrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
In a set operation, the specified value is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the length of the specified value is not equal to [`DieFledermausStream.BlockByteCount`](#property-systemint32-blockbytecount--get-).

--------------------------------------------------

## Property: `System.Byte[] Salt { get; set; }`
Gets and sets the salt used to help obfuscate the key when setting the password.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current stream is in write-mode.

-OR-

In a set operation, the current stream is not encrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
In a set operation, the specified value is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the length of the specified value is less than the maximum key length specified by [`DieFledermausStream.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get-).

--------------------------------------------------

## Property: `System.Byte[] Key { get; set; }`
Gets and sets the key used to encrypt the DieFledermaus stream.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current stream is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
In a set operation, the current stream is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
In a set operation, the specified value is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the specified value is an invalid length according to [`DieFledermausStream.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get-).

--------------------------------------------------

## Method: `public System.Boolean IsValidKeyByteSize(System.Int32 byteCount)`
Determines whether the specified value is a valid length for [`DieFledermausStream.Key`](#property-systembyte-key--get-set-), in bytes.
* `byteCount`: The number of bytes to test.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `byteCount` is a valid byte count according to [`DieFledermausStream.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get-); `false` if `byteCount` is invalid, or if the current instance is not encrypted.

--------------------------------------------------

## Method: `public System.Boolean IsValidKeyBitSize(System.Int32 bitCount)`
Determines whether the specified value is a valid length for [`DieFledermausStream.Key`](#property-systembyte-key--get-set-), in bits.
* `bitCount`: The number of bits to test.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `bitCount` is a valid bit count according to [`DieFledermausStream.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get-); `false` if `bitCount` is invalid, or if the current instance is not encrypted.

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

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `bitCount` is a valid bit count according to [`DieFledermausStream.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get-); `false` if `bitCount` is invalid, or if the current instance is not encrypted.

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

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `value` is a valid filename; `false` if `value` has a length of 0, has a length greater than 256 UTF-8 bytes, contains unpaired surrogate characters, contains non-whitespace control characters (non-whitespace characters between `\u0000` and `\u001f` inclusive, or between `\u007f` and `\u009f` inclusive), contains only whitespace, or is "." or ".." (the "current directory" and "parent directory" identifiers).

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

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current stream is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current stream is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public void SetPassword(System.String password, System.Int32 keyByteSize)`
Sets [`DieFledermausStream.Key`](#property-systembyte-key--get-set-) to a value derived from the specified password, using the specified key size.
* `password`: The password to set.
* `keyByteSize`: The length of [`DieFledermausStream.Key`](#property-systembyte-key--get-set-) to set, in bytes (1/8 the number of bits).

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current stream is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current stream is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`keyByteSize` is invalid according to [`DieFledermausStream.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get-).

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public void SetPassword(System.Security.SecureString password)`
Sets [`DieFledermausStream.Key`](#property-systembyte-key--get-set-) to a value derived from the specified password.
* `password`: The password to set.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current stream is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current stream is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public void SetPassword(System.Security.SecureString password, System.Int32 keyByteSize)`
Sets [`DieFledermausStream.Key`](#property-systembyte-key--get-set-) to a value derived from the specified password, using the specified key size.
* `password`: The password to set.
* `keyByteSize`: The length of [`DieFledermausStream.Key`](#property-systembyte-key--get-set-) to set, in bytes (1/8 the number of bits).

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current stream is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current stream is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`keyByteSize` is invalid according to [`DieFledermausStream.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get-).

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
This property indicates that the collection cannot be changed externally. If [`SettableOptions.IsFrozen`](#property-diefledermausdiefledermausstreamsettableoptionsisfrozen) is `false`, however, it may still be changed by the base stream.

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

--------------------------------------------------

# Type: `public enum DieFledermaus.LzmaDictionarySize`
Options for setting the LZMA dictionary size. A larger value alows a smaller compression size, but results in a higher memory usage when encoding and decoding and a longer encoding time.

--------------------------------------------------

## `LzmaDictionarySize.Default = 0`
The default value, [`LzmaDictionarySize.Size8m`](#lzmadictionarysizesize8m--8388608)

--------------------------------------------------

## `LzmaDictionarySize.Size16k = 16384`
16 kilobytes.

--------------------------------------------------

## `LzmaDictionarySize.Size64k = 65536`
64 kilobytes.

--------------------------------------------------

## `LzmaDictionarySize.Size1m = 1048576`
1 megabyte.

--------------------------------------------------

## `LzmaDictionarySize.Size2m = 2097152`
2 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.Size3m = 3145728`
3 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.Size4m = 4194304`
4 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.Size6m = 6291456`
6 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.Size8m = 8388608`
8 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.Size12m = 12582912`
12 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.Size16m = 16777216`
16 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.Size24m = 25165824`
24 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.Size32m = 33554432`
32 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.Size48m = 50331648`
48 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.Size64m = 67108864`
64 megabytes.

--------------------------------------------------

## `LzmaDictionarySize.MinValue = 16384`
The minimum value, equal to [`LzmaDictionarySize.Size16k`](#lzmadictionarysizesize16k--16384)

--------------------------------------------------

## `LzmaDictionarySize.MaxValue = 67108864`
The maximum value, equal to [`LzmaDictionarySize.Size64m`](#lzmadictionarysizesize64m--67108864).

--------------------------------------------------

# Type: `public class DieFledermaus.DieFledermauZArchive`
Represents a DieFledermauZ archive file.

### Remarks
If this class attempts to load a stream containing a valid [`DieFledermausStream`](#type-public-class-diefledermausdiefledermausstream), it will interpret the stream as an archive containing a single entry with the path set to the [`DieFledermausStream.Filename`](#property-systemstring-filename--get-set-), or a `null` path if the DieFledermaus stream does not have a filename set.

--------------------------------------------------

## Property: `System.IO.Stream BaseStream { get; }`
Gets the underlying stream used by the current instance.

--------------------------------------------------

## Constructor: `public DieFledermauZArchive(System.IO.Stream stream, DieFledermaus.MauZArchiveMode mode, System.Boolean leaveOpen)`
Creates a new instance using the specified options.
* `stream`: The stream containing the DieFledermauZ archive.
* `mode`: Indicates options for accessing the stream.
* `leaveOpen`: `true` to leave `stream` open when the current instance is disposed; `false` to close `stream`.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`mode` is [`MauZArchiveMode.Create`](#mauzarchivemodecreate--0), and `stream` does not support writing.

-OR-

`mode` is [`MauZArchiveMode.Read`](#mauzarchivemoderead--1), and `stream` does not support reading.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
`mode` is [`MauZArchiveMode.Read`](#mauzarchivemoderead--1), and `stream` does not contain either a valid DieFledermauZ archive or a valid [`DieFledermausStream`](#type-public-class-diefledermausdiefledermausstream).

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
`mode` is [`MauZArchiveMode.Read`](#mauzarchivemoderead--1), and `stream` contains unsupported options.

##### [`IOException`](https://msdn.microsoft.com/en-us/library/system.io.ioexception.aspx)
An I/O error occurred.

--------------------------------------------------

## Constructor: `public DieFledermauZArchive(System.IO.Stream stream, DieFledermaus.MauZArchiveMode mode)`
Creates a new instance using the specified options.
* `stream`: The stream containing the DieFledermauZ archive.
* `mode`: Indicates options for accessing the stream.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`mode` is [`MauZArchiveMode.Create`](#mauzarchivemodecreate--0), and `stream` does not support writing.

-OR-

`mode` is [`MauZArchiveMode.Read`](#mauzarchivemoderead--1), and `stream` does not support reading.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
`mode` is [`MauZArchiveMode.Read`](#mauzarchivemoderead--1), and `stream` does not contain either a valid DieFledermauZ archive or a valid [`DieFledermausStream`](#type-public-class-diefledermausdiefledermausstream).

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
`mode` is [`MauZArchiveMode.Read`](#mauzarchivemoderead--1), and `stream` contains unsupported options.

##### [`IOException`](https://msdn.microsoft.com/en-us/library/system.io.ioexception.aspx)
An I/O error occurred.

--------------------------------------------------

## Constructor: `public DieFledermauZArchive(System.IO.Stream stream, DieFledermaus.MausEncryptionFormat encryptionFormat, System.Boolean leaveOpen)`
Creates a new instance in create-mode using the specified encryption format.
* `stream`: The stream containing the DieFledermauZ archive.
* `encryptionFormat`: Indicates options for how to encrypt the stream.
* `leaveOpen`: `true` to leave `stream` open when the current instance is disposed; `false` to close `stream`.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

##### [`IOException`](https://msdn.microsoft.com/en-us/library/system.io.ioexception.aspx)
An I/O error occurred.

--------------------------------------------------

## Constructor: `public DieFledermauZArchive(System.IO.Stream stream, DieFledermaus.MausEncryptionFormat encryptionFormat)`
Creates a new instance in create-mode using the specified encryption format.
* `stream`: The stream containing the DieFledermauZ archive.
* `encryptionFormat`: Indicates options for how to encrypt the stream.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`stream` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`stream` does not support writing.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
`stream` is closed.

##### [`IOException`](https://msdn.microsoft.com/en-us/library/system.io.ioexception.aspx)
An I/O error occurred.

--------------------------------------------------

## Method: `public void Decrypt()`
Decrypts the current instance.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is in write-only mode.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
The stream contained invalid data.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The stream contained unsupported optoins.

##### [`IOException`](https://msdn.microsoft.com/en-us/library/system.io.ioexception.aspx)
An I/O error occurred.

##### [`CryptographicException`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.cryptographicexception.aspx)
[`DieFledermauZItem.Key`](#property-systembyte-key--get-set--2) is not set to the correct value. It is safe to attempt to call [`DieFledermauZArchive.Decrypt()`](#method-public-void-decrypt) again if this exception is caught.

--------------------------------------------------

## Property: `DieFledermaus.DieFledermauZArchive.EntryList Entries { get; }`
Gets a collection containing all entries in the current archive.

--------------------------------------------------

## Property: `DieFledermaus.MauZArchiveMode Mode { get; }`
Gets the mode of operation of the current instance.

--------------------------------------------------

## Property: `DieFledermaus.MausEncryptionFormat EncryptionFormat { get; }`
Gets the encryption format of the current instance.

--------------------------------------------------

## Property: `System.Security.Cryptography.KeySizes KeySizes { get; }`
Gets a [`KeySizes`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.keysizes.aspx) object indicating all valid key sizes for the current encryption, or `null` if the current archive is not encrypted.

--------------------------------------------------

## Property: `System.Int32 BlockSize { get; }`
Gets the number of bits in a single block of encrypted data, or 0 if the current instance is not encrypted.

--------------------------------------------------

## Property: `System.Int32 BlockByteCount { get; }`
Gets the number of bytes in a single block of encrypted data, or 0 if the current instance is not encrypted.

--------------------------------------------------

## Property: `System.Byte[] IV { get; set; }`
Gets and sets the initialization vector used when encrypting the current instance.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current archive is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current archive is in write-mode.

-OR-

In a set operation, the current archive is not encrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
In a set operation, the specified value is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the length of the specified value is not equal to [`DieFledermauZArchive.BlockByteCount`](#property-systemint32-blockbytecount--get--1).

--------------------------------------------------

## Property: `System.Byte[] Salt { get; set; }`
Gets and sets the salt used to help obfuscate the key when setting the password.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current archive is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current archive is in write-mode.

-OR-

In a set operation, the current archive is not encrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
In a set operation, the specified value is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the length of the specified value is less than the maximum key length specified by [`DieFledermauZArchive.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get--1).

--------------------------------------------------

## Property: `System.Byte[] Key { get; set; }`
Gets and sets the key used to encrypt the DieFledermaus stream.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current archive is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current archive is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
In a set operation, the current archive is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
In a set operation, the specified value is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the specified value is an invalid length according to [`DieFledermauZArchive.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get--1).

--------------------------------------------------

## Method: `public void SetPassword(System.String password)`
Sets [`DieFledermauZArchive.Key`](#property-systembyte-key--get-set--1) to a value derived from the specified password.
* `password`: The password to set.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current archive is disposed.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current archive is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public void SetPassword(System.String password, System.Int32 keyByteSize)`
Sets [`DieFledermauZArchive.Key`](#property-systembyte-key--get-set--1) to a value derived from the specified password, using the specified key size.
* `password`: The password to set.
* `keyByteSize`: The length of [`DieFledermauZArchive.Key`](#property-systembyte-key--get-set--1) to set, in bytes (1/8 the number of bits).

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current archive is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current archive is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current archive is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`keyByteSize` is invalid according to [`DieFledermauZArchive.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get--1).

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public void SetPassword(System.Security.SecureString password)`
Sets [`DieFledermauZArchive.Key`](#property-systembyte-key--get-set--1) to a value derived from the specified password.
* `password`: The password to set.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current archive is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current archive is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current archive is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public void SetPassword(System.Security.SecureString password, System.Int32 keyByteSize)`
Sets [`DieFledermauZArchive.Key`](#property-systembyte-key--get-set--1) to a value derived from the specified password, using the specified key size.
* `password`: The password to set.
* `keyByteSize`: The length of [`DieFledermauZArchive.Key`](#property-systembyte-key--get-set--1) to set, in bytes (1/8 the number of bits).

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current archive is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current archive is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current archive is in read-mode and the stream has already been successfully decrypted.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`keyByteSize` is invalid according to [`DieFledermauZArchive.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get--1).

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public DieFledermaus.DieFledermauZArchiveEntry Create(System.String path, DieFledermaus.MausCompressionFormat compressionFormat, DieFledermaus.MausEncryptionFormat encryptionFormat)`
Adds a new [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry) to the current archive.
* `path`: The path to the entry within the archive's file structure.
* `compressionFormat`: The compression format of the archive entry.
* `encryptionFormat`: The encryption format of the archive entry.

**Returns:**  Type [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry): The newly-created [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry) object.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is in read-only mode.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`path` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionFormat` is not a valid [`MausCompressionFormat`](#type-public-enum-diefledermausmauscompressionformat) value.

-OR-

`encryptionFormat` is not a valid [`MausEncryptionFormat`](#type-public-enum-diefledermausmausencryptionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`path` is not a valid file path.

-OR-

`path` already exists.

--------------------------------------------------

## Method: `public DieFledermaus.DieFledermauZArchiveEntry Create(System.String path, DieFledermaus.MausCompressionFormat compressionFormat)`
Adds a new [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry) to the current archive.
* `path`: The path to the entry within the archive's file structure.
* `compressionFormat`: The compression format of the archive entry.

**Returns:**  Type [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry): The newly-created [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry) object.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is in read-only mode.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`path` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`compressionFormat` is not a valid [`MausCompressionFormat`](#type-public-enum-diefledermausmauscompressionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`path` is not a valid file path.

-OR-

`path` already exists.

### Remarks
If `path` contains any existing empty directories as one of its subdirectories, this method will remove the existing (no-longer-)empty directories.

--------------------------------------------------

## Method: `public DieFledermaus.DieFledermauZArchiveEntry Create(System.String path, DieFledermaus.MausEncryptionFormat encryptionFormat)`
Adds a new [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry) to the current archive.
* `path`: The path to the entry within the archive's file structure.
* `encryptionFormat`: The encryption format of the archive entry.

**Returns:**  Type [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry): The newly-created [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry) object.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is in read-only mode.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`path` is `null`.

##### [`InvalidEnumArgumentException`](https://msdn.microsoft.com/en-us/library/system.componentmodel.invalidenumargumentexception.aspx)
`encryptionFormat` is not a valid [`MausEncryptionFormat`](#type-public-enum-diefledermausmausencryptionformat) value.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`path` is not a valid file path.

-OR-

`path` already exists.

### Remarks
If `path` contains any existing empty directories as one of its subdirectories, this method will remove the existing (no-longer-)empty directories.

--------------------------------------------------

## Method: `public DieFledermaus.DieFledermauZArchiveEntry Create(System.String path)`
Adds a new [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry) to the current archive.
* `path`: The path to the entry within the archive's file structure.

**Returns:**  Type [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry): The newly-created [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry) object.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is in read-only mode.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`path` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`path` is not a valid file path.

-OR-

`path` already exists.

### Remarks
If `path` contains any existing empty directories as one of its subdirectories, this method will remove the existing (no-longer-)empty directories.

--------------------------------------------------

## Method: `public DieFledermaus.DieFledermauZEmptyDirectory AddEmptyDirectory(System.String path)`
Adds a new empty directory to the current archive.
* `path`: The path to the empty directory within the archive's file structure.

**Returns:**  Type [`DieFledermauZEmptyDirectory`](#type-public-class-diefledermausdiefledermauzemptydirectory): A newly-created [`DieFledermauZEmptyDirectory`](#type-public-class-diefledermausdiefledermauzemptydirectory) object.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`path` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`path` is not a valid file path.

-OR-

`path` already exists.

##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is in read-only mode.

### Remarks
If `path` contains any existing empty directories as one of its subdirectories, this method will remove the existing (no-longer-)empty directories.

--------------------------------------------------

## Method: `public static System.Boolean IsValidFilePath(System.String path)`
Determines if the specified value is a valid value for a file path.
* `path`: The value to test.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `path` is a valid path; `false` if an element in `path` has a length of 0, has a length greater than 256 UTF-8 bytes, contains unpaired surrogate characters, contains non-whitespace control characters (non-whitespace characters between `\u0000` and `\u001f` inclusive, or between `\u007f` and `\u009f` inclusive), contains only whitespace, or is "." or ".." (the "current directory" and "parent directory" identifiers).

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`path` is `null`.

--------------------------------------------------

## Method: `public static System.Boolean IsValidEmptyDirectoryPath(System.String path)`
Determines if the specified value is a valid value for an empty directory path.
* `path`: The value to test.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `path` is a valid path; `false` if an element in `path` has a length of 0, has a length greater than 256 UTF-8 bytes, contains unpaired surrogate characters, contains non-whitespace control characters (non-whitespace characters between `\u0000` and `\u001f` inclusive, or between `\u007f` and `\u009f` inclusive), contains only whitespace, or is "." or ".." (the "current directory" and "parent directory" identifiers).

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`path` is `null`.

--------------------------------------------------

## Method: `public void Dispose()`
Releases all resources used by the current instance.

--------------------------------------------------

# Type: `public class DieFledermaus.DieFledermauZArchive.EntryList`
Represents a list of [`DieFledermauZItem`](#type-public-abstract-class-diefledermausdiefledermauzitem) objects.

--------------------------------------------------

## Property: `DieFledermauZArchive.EntryList.Item(System.Int32 index)`
Get the element at the specified index.
* `index`: The index of the element to get.

### Exceptions
##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`index` is less than 0 or is greater than [`EntryList.Count`](#property-virtual-systemint32-count--get--1).

--------------------------------------------------

## Property: `virtual System.Int32 Count { get; }`
Gets the number of elements in the list.

--------------------------------------------------

## Property: `DieFledermaus.DieFledermauZArchive.EntryList.PathCollection Paths { get; }`
Gets a collection containing all filenames and directory names in the current instance.

--------------------------------------------------

## Method: `DieFledermauZArchive.EntryList.TryGetEntry(System.String path, out DieFledermaus.DieFledermauZItem value)`
Gets the entry associated with the specified path.
* `path`: The path to search for in the archive.
* `value`: When this method returns, contains the value associated with `path`, or `null` if `path` was not found. This parameter is passed uninitialized.

**Returns:** `true` if `path` was found; `false` otherwise.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`path` is `null`.

--------------------------------------------------

## Property: `System.Boolean IsFrozen { get; }`
Gets a value indicating whether the current instance is entirely frozen against all further changes.

--------------------------------------------------

## Method: `public System.Int32 IndexOf(DieFledermaus.DieFledermauZItem item)`
Returns the index of the specified element.
* `item`: The element to search for in the list.

**Returns:**  Type [`Int32`](https://msdn.microsoft.com/en-us/library/system.int32.aspx): The index of `item`, if found; otherwise, `null`.

--------------------------------------------------

## Method: `public System.Boolean Contains(DieFledermaus.DieFledermauZItem item)`
Determines whether the specified element exists in the list.
* `item`: The element to search for in the list.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `item` was found; `false` otherwise.

--------------------------------------------------

## Method: `public void CopyTo(DieFledermaus.DieFledermauZItem[] array, System.Int32 index)`
Copies all elements in the collection to the specified array, starting at the specified index.
* `array`: The array to which the collection will be copied. The array must have zero-based indexing.
* `index`: The index in `array` at which copying begins.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`array` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`index` is less than 0.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`index` plus [`EntryList.Count`](#property-virtual-systemint32-count--get--1) is greater than the length of `array`.

--------------------------------------------------

## Method: `public DieFledermaus.DieFledermauZArchive.EntryList.Enumerator GetEnumerator()`
Returns an enumerator which iterates through the collection.

**Returns:**  Type [`Enumerator`](#type-public-struct-diefledermausdiefledermauzarchiveentrylistenumerator): An enumerator which iterates through the collection.

--------------------------------------------------

# Type: `public struct DieFledermaus.DieFledermauZArchive.EntryList.Enumerator`
An enumerator which iterates through the collection.

--------------------------------------------------

## Property: `DieFledermaus.DieFledermauZItem Current { get; }`
Gets the element at the current position in the enumerator.

--------------------------------------------------

## Method: `public void Dispose()`
Disposes of the current instance.

--------------------------------------------------

## Method: `public System.Boolean MoveNext()`
Advances the enumerator to the next position in the collection.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if the enumerator was successfully advanced; `false` if the enumerator has passed the end of the collection.

--------------------------------------------------

# Type: `public class DieFledermaus.DieFledermauZArchive.EntryList.PathCollection`
A collection containing the paths of all entries in the collection.

--------------------------------------------------

## Property: `DieFledermauZArchive.EntryList.PathCollection.Item(System.Int32 index)`
Gets the element at the specified index.
* `index`: The element at the specified index.

### Exceptions
##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`index` is less than 0 or is greater than or equal to [`PathCollection.Count`](#property-virtual-systemint32-count--get--2).

--------------------------------------------------

## Property: `virtual System.Int32 Count { get; }`
Gets the number of elements contained in the list.

--------------------------------------------------

## Property: `System.Boolean IsFrozen { get; }`
Gets a value indicating whether the current instance is entirely frozen against all further changes.

--------------------------------------------------

## Method: `public System.Int32 IndexOf(System.String path)`
Returns the index of the specified path.
* `path`: The path to search for in the list.

**Returns:**  Type [`Int32`](https://msdn.microsoft.com/en-us/library/system.int32.aspx): The index of `path`, if found; otherwise, -1.

--------------------------------------------------

## Method: `public System.Boolean Contains(System.String path)`
Gets a value indicating whether the specified path exists in the list.
* `path`: The path to search for in the list.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if `path` was found; `false` otherwise.

--------------------------------------------------

## Method: `public void CopyTo(System.String[] array, System.Int32 index)`
Copies all elements in the collection to the specified array, starting at the specified index.
* `array`: The array to which the collection will be copied. The array must have zero-based indexing.
* `index`: The index in `array` at which copying begins.

### Exceptions
##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`array` is `null`.

##### [`ArgumentOutOfRangeException`](https://msdn.microsoft.com/en-us/library/system.argumentoutofrangeexception.aspx)
`index` is less than 0.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`index` plus [`PathCollection.Count`](#property-virtual-systemint32-count--get--2) is greater than the length of `array`.

--------------------------------------------------

## Method: `public DieFledermaus.DieFledermauZArchive.EntryList.PathCollection.Enumerator GetEnumerator()`
Returns an enumerator which iterates through the collection.

**Returns:**  Type [`Enumerator`](#type-public-struct-diefledermausdiefledermauzarchiveentrylistpathcollectionenumerator): An enumerator which iterates through the collection.

--------------------------------------------------

# Type: `public struct DieFledermaus.DieFledermauZArchive.EntryList.PathCollection.Enumerator`
An enumerator which iterates through the collection.

--------------------------------------------------

## Property: `System.String Current { get; }`
Gets the element at the current position in the enumerator.

--------------------------------------------------

## Method: `public void Dispose()`
Disposes of the current instance.

--------------------------------------------------

## Method: `public System.Boolean MoveNext()`
Advances the enumerator to the next position in the collection.

**Returns:**  Type [`Boolean`](https://msdn.microsoft.com/en-us/library/system.boolean.aspx): `true` if the enumerator was successfully advanced; `false` if the enumerator has passed the end of the collection.

--------------------------------------------------

# Type: `public enum DieFledermaus.MauZArchiveMode`
Indicates options for a [`DieFledermauZArchive`](#type-public-class-diefledermausdiefledermauzarchive).

--------------------------------------------------

## `MauZArchiveMode.Create = 0`
The [`DieFledermauZArchive`](#type-public-class-diefledermausdiefledermauzarchive) is in write-only mode.

--------------------------------------------------

## `MauZArchiveMode.Read = 1`
The [`DieFledermauZArchive`](#type-public-class-diefledermausdiefledermauzarchive) is in read-only mode.

--------------------------------------------------

# Type: `public class DieFledermaus.DieFledermauZArchiveEntry`
Represents a single file entry in a [`DieFledermauZArchive`](#type-public-class-diefledermausdiefledermauzarchive).

--------------------------------------------------

## Property: `DieFledermaus.MausCompressionFormat CompressionFormat { get; }`
Gets the compression format of the current instance.

--------------------------------------------------

## Property: `DieFledermaus.DieFledermausStream.SettableOptions EncryptedOptions { get; }`
Gets a collection containing options which should be encrypted, or `null` if the current entry is not encrypted.

--------------------------------------------------

## Method: `public System.IO.Stream OpenWrite()`
Opens the archive entry for writing.

**Returns:**  Type [`Stream`](https://msdn.microsoft.com/en-us/library/system.io.stream.aspx): A writeable stream to which the data will be written..

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance has been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in read-only mode.

-OR-

The current instance has already been open for writing.

--------------------------------------------------

## Method: `public override DieFledermaus.DieFledermauZItem Decrypt()`
Decrypts the current instance.

**Returns:**  Type [`DieFledermauZItem`](#type-public-abstract-class-diefledermausdiefledermauzitem): The current instance, in decrypted form.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance has been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in write-only mode.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current instance is not encrypted.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
The stream contains invalid data.

##### [`CryptographicException`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.cryptographicexception.aspx)
[`DieFledermauZItem.Key`](#property-systembyte-key--get-set--2) is not set to the correct value. It is safe to attempt to call [`DieFledermauZArchiveEntry.Decrypt()`](#method-public-override-diefledermausdiefledermauzitem-decrypt) or [`DieFledermauZArchiveEntry.OpenRead()`](#method-public-systemiostream-openread) again if this exception is caught.

--------------------------------------------------

## Method: `public System.IO.Stream OpenRead()`
Opens the archive entry for reading.

**Returns:**  Type [`Stream`](https://msdn.microsoft.com/en-us/library/system.io.stream.aspx):

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance has been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in write-only mode.

--------------------------------------------------

# Type: `public class DieFledermaus.DieFledermauZEmptyDirectory`
Represents an empty directory in

--------------------------------------------------

## Property: `System.Boolean EncryptPath { get; set; }`
Gets and sets a value indicating whether the filename will be encrypted within the current instance.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in read-only mode.

### Remarks
Setting this property to `true` will set [`DieFledermauZItem.Key`](#property-systembyte-key--get-set--2), [`DieFledermauZItem.IV`](#property-systembyte-iv--get-set--2), and [`DieFledermauZItem.Salt`](#property-systembyte-salt--get-set--2) to randomly-generated values. Subsequently setting this property to `false` will set these properties to `null`, and the old values will not be remembered or saved.

--------------------------------------------------

## Method: `public override DieFledermaus.DieFledermauZItem Decrypt()`
Decrypts the current instance.

**Returns:**  Type [`DieFledermauZItem`](#type-public-abstract-class-diefledermausdiefledermauzitem):

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance has been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in write-only mode.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current instance is not encrypted.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
The stream contains invalid data.

##### [`CryptographicException`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.cryptographicexception.aspx)
[`DieFledermauZItem.Key`](#property-systembyte-key--get-set--2) is not set to the correct value. It is safe to attempt to call [`DieFledermauZEmptyDirectory.Decrypt()`](#method-public-override-diefledermausdiefledermauzitem-decrypt-1) again if this exception is caught.

--------------------------------------------------

# Type: `public abstract class DieFledermaus.DieFledermauZItem`
Represents a single entry in a [`DieFledermauZArchive`](#type-public-class-diefledermausdiefledermauzarchive).

--------------------------------------------------

## Property: `DieFledermaus.DieFledermauZArchive Archive { get; }`
Gets the [`DieFledermauZArchive`](#type-public-class-diefledermausdiefledermauzarchive) containing the current instance, or `null` if the current instance has been deleted.

--------------------------------------------------

## Property: `System.String Path { get; }`
Gets the path of the current instance within the archive.

--------------------------------------------------

## Method: `public virtual DieFledermaus.DieFledermauZItem Decrypt()`
Decrypts the current instance.

**Returns:**  Type [`DieFledermauZItem`](#type-public-abstract-class-diefledermausdiefledermauzitem):

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance has been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in write-only mode.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current instance is not encrypted.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
The stream contains invalid data.

##### [`CryptographicException`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.cryptographicexception.aspx)
[`DieFledermauZItem.Key`](#property-systembyte-key--get-set--2) is not set to the correct value. It is safe to attempt to call [`DieFledermauZItem.Decrypt()`](#method-public-virtual-diefledermausdiefledermauzitem-decrypt) again if this exception is caught.

--------------------------------------------------

## Property: `DieFledermaus.MausEncryptionFormat EncryptionFormat { get; }`
Gets the encryption format of the current instance.

--------------------------------------------------

## Property: `System.Byte[] Key { get; set; }`
Gets and sets the encryption key for the current instance, or `null` if the current instance is not encrypted.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current instance has been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
In a set operation, [`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in read-mode, and the current instance has already been successfully decoded.

--------------------------------------------------

## Property: `System.Byte[] IV { get; set; }`
Gets and sets the initialization vector used for the current instance, or `null` if the current instance is not encrypted.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current instance has been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is not encrypted.

-OR-

The current instance is in read-mode.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
In a set operation, the specified value is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the specified value is the wrong length.

--------------------------------------------------

## Property: `System.Byte[] Salt { get; set; }`
Gets and sets the salt used for the current instance, or `null` if the current instance is not encrypted.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current instance has been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is not encrypted.

-OR-

The current instance is in read-mode.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
In a set operation, the specified value is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
In a set operation, the specified value is the wrong length.

--------------------------------------------------

## Method: `public void SetPassword(System.String password)`
Sets [`DieFledermauZItem.Key`](#property-systembyte-key--get-set--2) to a value derived from the specified password.
* `password`: The password to set.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current instance has been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
In a set operation, [`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in read-mode, and the current instance has already been successfully decoded.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public void SetPassword(System.Security.SecureString password)`
Sets [`DieFledermauZItem.Key`](#property-systembyte-key--get-set--2) to a value derived from the specified password.
* `password`: The password to set.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current instance has been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current instance is not encrypted.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
In a set operation, [`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in read-mode, and the current instance has already been successfully decoded.

##### [`ArgumentNullException`](https://msdn.microsoft.com/en-us/library/system.argumentnullexception.aspx)
`password` is `null`.

##### [`ArgumentException`](https://msdn.microsoft.com/en-us/library/system.argumentexception.aspx)
`password` has a length of 0.

--------------------------------------------------

## Method: `public void Delete()`
Deletes the current instance.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance has already been deleted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in read-only mode.

--------------------------------------------------

## Method: `public override System.String ToString()`
Returns a string representation of the current instance.

**Returns:**  Type [`String`](https://msdn.microsoft.com/en-us/library/system.string.aspx): A string representation of the current instance.

--------------------------------------------------

# Type: `public class DieFledermaus.DieFledermauZItemUnknown`
A [`DieFledermauZArchive`](#type-public-class-diefledermausdiefledermauzarchive) entry with an encrypted filename, which is currently unknown whether it represents a file or an empty directory. Use [`DieFledermauZItemUnknown.Decrypt()`](#method-public-override-diefledermausdiefledermauzitem-decrypt-2) after setting the key or password.

--------------------------------------------------

## Method: `public override DieFledermaus.DieFledermauZItem Decrypt()`
Decrypts the current instance and replaces it in [`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) with a properly decrypted instance.

**Returns:**  Type [`DieFledermauZItem`](#type-public-abstract-class-diefledermausdiefledermauzitem): Either a decrypted [`DieFledermauZArchiveEntry`](#type-public-class-diefledermausdiefledermauzarchiveentry) object, or a decrypted [`DieFledermauZEmptyDirectory`](#type-public-class-diefledermausdiefledermauzemptydirectory) object, which will replace the current instance in [`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-).

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current instance has already been successfully decrypted.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
[`DieFledermauZItem.Archive`](#property-diefledermausdiefledermauzarchive-archive--get-) is in write-only mode.

##### [`InvalidOperationException`](https://msdn.microsoft.com/en-us/library/system.invalidoperationexception.aspx)
The current instance is not encrypted.

##### [`InvalidDataException`](https://msdn.microsoft.com/en-us/library/system.io.invaliddataexception.aspx)
The stream contains invalid data.

##### [`CryptographicException`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.cryptographicexception.aspx)
[`DieFledermauZItem.Key`](#property-systembyte-key--get-set--2) is not set to the correct value. It is safe to attempt to call [`DieFledermauZItemUnknown.Decrypt()`](#method-public-override-diefledermausdiefledermauzitem-decrypt-2) again if this exception is caught.
