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
* `encryptionFormat`: Indicates the format of the encryption.
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
* `encryptionFormat`: Indicates the format of the encryption.

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
Creates a new instance in write-mode, with the specified compression and encryption formats.
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
Creates a new instance in write-mode, with the specified compression and encryption formats.
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
* `encryptionFormat`: Indicates the format of the encryption.
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
* `encryptionFormat`: Indicates the format of the encryption.

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
Creates a new instance in write-mode with the specified compression level.
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
Creates a new instance in write-mode with the specified compression level.
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
Creates a new instance in write-mode with the specified compression level and encryption format.
* `stream`: The stream to which compressed data will be written.
* `compressionLevel`: Indicates the compression level of the stream.
* `encryptionFormat`: Indicates the format of the compression mode.
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
Creates a new instance in write-mode with the specified compression level and encryption format.
* `stream`: The stream to which compressed data will be written.
* `compressionLevel`: Indicates the compression level of the stream.
* `encryptionFormat`: Indicates the format of the encryption.

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
In a set operation, the specified value is an invalid length according to [`DieFledermausStream.KeySizes`](#property-systemsecuritycryptographykeysizes-keysizes--get-).

--------------------------------------------------

## Property: `System.Int32 BlockSize { get; }`
Gets the number of bits in a single block of encrypted data, or 0 if the current instance is not encrypted.

--------------------------------------------------

## Property: `System.Int32 BlockByteCount { get; }`
Gets the number of bytes in a single block of encrypted data, or 0 if the current instance is not encrypted.

--------------------------------------------------

## Property: `System.Boolean EncryptFilename { get; set; }`
Gets and sets a value indicating whether [`DieFledermausStream.Filename`](#property-systemstring-filename--get-set-) should be encrypted.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
In a set operation, the current instance is disposed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
In a set operation, the current instance is in read-mode.

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
Attempts to pre-load the data in the current instance, and test whether [`DieFledermausStream.Key`](#property-systembyte-key--get-set-) is set to the correct value if the current stream is encrypted and to decrypt [`DieFledermausStream.Filename`](#property-systemstring-filename--get-set-) if [`DieFledermausStream.EncryptFilename`](#property-systemboolean-encryptfilename--get-set-) is `true`.

### Exceptions
##### [`ObjectDisposedException`](https://msdn.microsoft.com/en-us/library/system.objectdisposedexception.aspx)
The current stream is closed.

##### [`NotSupportedException`](https://msdn.microsoft.com/en-us/library/system.notsupportedexception.aspx)
The current stream is in write-mode.

##### [`CryptographicException`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.cryptographicexception.aspx)
[`DieFledermausStream.Key`](#property-systembyte-key--get-set-) is not set to the correct value.

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
[`DieFledermausStream.Key`](#property-systembyte-key--get-set-) is not set to the correct value.

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

## Property: `System.Security.Cryptography.KeySizes KeySizes { get; }`
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
