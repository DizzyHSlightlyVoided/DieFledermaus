DieFledermaus format (.maus file)
=================================
Version 0.97
------------
* File Extension: ".maus"
* Byte order: little-endian
* Signing form: two's complement

The DieFledermaus file format is simply a [DEFLATE](http://en.wikipedia.org/wiki/DEFLATE)-compressed file, with metadata and a magic number. The name exists solely to be a bilingual pun.

Terminology
-----------
* **decompressed file** or **decompressed data:** The file as it originally existed before compression.
* **compressed file** or **compressed data:** The file as it exists after compression, but before encryption. This is referred to as such even if the compression-mode is *none*.
* **encoder:** Any application, library, or other software which encodes data to a DieFledermaus stream.
* **decoder:** Any application, library, or other software which restores the data in a DieFledermaus stream to its original form.
* **re-encoder:** Any software which functions as both an encoder and a decoder.
* **length-prefixed string:** In the DieFledermaus format, a length-prefixed string is a sequence of bytes, usually UTF-8 text, which is prefixed by the *length value*, an 8-bit or 16-bit unsigned integer indicating the length of the string (not including the length value itself). If the length value is 0, the actual length of the string is 256 for 8-bit length values and 65536 for 16-bit length values.

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED",  "MAY", and "OPTIONAL" in this document are to be interpreted as described in [RFC 2119](https://www.ietf.org/rfc/rfc2119.txt).

Structure
---------
Any encoder or decoder must support version 0.97 at minimum. A decoder must be able to support any non-depreciated version, but an encoder may only support a single version. A re-encoder should encode using the highest version understood by the decoder.

When encoding a file to a DieFledermaus archive, the filename of the DieFledermaus file should be the same as the file to encode but with the extension ".maus" added to the end, unless a specific filename is requested by the user.

A DieFledermaus stream contains the following fields:

1. **Magic Number:** "`mAuS`" (`6d 41 75 53`)
2. **Version:** An unsigned 16-bit value containing the version number in fixed-point form; divide the integer value by 100 to get the actual version number, i.e. `5f 00` (hex) = integer `95` (decimal) = version 0.95.
3. **Format:** An array of length-prefixed strings describing the format.
4. **Compressed Length:** A signed 64-bit integer containing the number of bytes in the compressed data.
5. **Decompressed Length:** A signed 64-bit integer containing the number of bytes in the uncompressed data. If the compressed data stream decodes to a length greater than this value, the extra data is discarded. The minimum length of the decompressed data must be 1 byte.
6. **Checksum:** A SHA-512 hash of the decompressed value.
7. **Data:** The compressed data itself.

### Format
**Format** is an array of 16-bit length-prefixed strings, used to specify information about the format of the encoded data. The field starts with the **Format Length**, an unsigned 16-bit integer specifying the number of elements in the array; unlike the length-prefixed strings themselves, a 0-value in the **Format Length** means that there really are zero elements.

Some elements in **Format** require more information than just the current value in order to behave properly. For example, the `AES` element specifies that the archive is AES-encrypted, but does not indicate the key size. The next element or elements must be used as *parameters* for the element; the original element is thus a *parameterized element*. Format elements should have a length no greater than 256 bytes because I mean seriously c'mon. Parameters may be any length between 1 and 65536 inclusive.

If no element in **Format** specifies the compression format, the decoder must use the DEFLATE algorithm.

The following values are defined for the default implementation:
* `Name` - *One parameter.* Indicates that the compressed file has a filename, specified in the parameter. Filenames must not contain forward-slashes (`/`, hex `2f`), non-whitespace control characters (non-whitespace characters between `00` and `1f` inclusive or between `7f` and `9f` inclusive), or invalid surrogate characters. Filenames must contain at least one non-whitespace character, and cannot be the "current directory" identifier "." (a single period) or "parent directory" identifier ".." (two periods). If no filename is specified, the decoder should assume that the filename is the same as the DieFledermaus file without the ".maus" extension. The maximum length of the filename is 256 UTF-8 bytes.
* `NK` - *No parameters.* **N**icht **K**omprimiert ("not compressed"). Indicates that the file is not compressed.
* `DEF` - *No parameters.* Indicates that the file is compressed using the [DEFLATE](http://en.wikipedia.org/wiki/DEFLATE) algorithm.
* `LZMA` - *No parameters.* Indicates that the file is compressed using the [Lempel-Ziv-Markov chain algorithm](https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Markov_chain_algorithm). Like DEFLATE, LZMA is based on the [LZ77 algorithm](https://en.wikipedia.org/wiki/LZ77_and_LZ78). The format of the LZMA stream is the 5-byte header, followed by every block in the stream. Due to the limitations of the .Net Framework implementation of LZMA, the dictionary size must be less than or equal to 64 megabytes.
* `AES` - *One parameter.* The file is AES-encrypted. To indicate the key length, the parameter must be either the three-byte string "128" (that is, a string containing the ASCII characters "1" (`0x31`), "2" (`0x32`), and "8" (`0x38`)), "192", or "256"; or a 16-bit integer (2 bytes) in little-endian order equal to 128, 192, or 256.
* `DeL` - *One parameter.* **De**compressed **L**ength, or 
**De**komprimierte **L**änge. The parameter is a signed 64-bit integer containing the number of bytes in the uncompressed data. If the archive is not encrypted, this value must be equal to **Decompressed Length**. This value should be included in the **Encrypted Format** array when the archive is encrypted.
* `Ers` - *One parameter.* **Ers**tellt, "created". Indicates when the file to compress was originally created. The time is in UTC form, and is stored as a 64-bit integer containing the number of [.Net Framework "ticks" (defined as 100 nanoseconds) since 0001-01-01T00:00:00Z](https://msdn.microsoft.com/en-us/library/system.datetime.ticks.aspx), excluding leap seconds. The minimum value is 0 (or 0001-01-01T00:00:00Z), and the maximum value is 9999-12-31T23:59:59.9999999Z.
* `Mod` - *One parameter.* **Mod**ified, or **mod**ifiziert. Indicates when the file to compress was last modified. Same format as `Ers`.
* `Kom` - *Between 2 and 257 parameters* **Kom**mentar ("comment"). A textual comment. The first parameter is the number of remaining parameters, and a value of 0 means 256. The remaining parameters contain the UTF-8 text of the comment. All but the last parameter must contain 256 bytes. All told, the length of the comment must be less than or equal to 65536. Beyond that, the comment can be anything.

If a decoder encounters contradictory values (i.e. both `NC` and `DEF`), it should stop attempting to decode the file, rather than trying to guess what to use, and should clearly inform the user of this error. If a decoder encounters redundant values (i.e. both `NC` and `NK`, or two `Name` items which are each followed by the same filename), the duplicates should be ignored.

A decoder must not attempt to decode an archive if it finds any unexpected or unknown values in the **Format** field; that doesn't make sense. It should, however, attempt to decode any *known* format, regardless of the file's version number.

Encryption
----------
DieFledermaus supports [AES encryption](http://en.wikipedia.org/wiki/Advanced_Encryption_Standard), with 256, 192, and 128-bit keys. Both text-based passwords and raw binary keys are supported. An encoder may use only text-based passwords without any options for setting binary keys, but a decoder must allow both passwords and binary keys.

An encoder should use 256-bit keys, as they are the most secure. A decoder must be able to decode all key sizes, of course.

### Changes to the format
When a DieFledermaus archive is encrypted, the following DieFledermaus fields behave slightly differently:
* **Decompressed Length** is replaced with the **PBKDF2 Value**, which is still a signed 64-bit integer to make the structure more straightforward. This value is the number of [PBKDF2](https://en.wikipedia.org/wiki/PBKDF2) cycles, minus 9001. The number of cycles must be between 9001 and 2147483647 inclusive; therefore, the field must have a value between 0 and 2147474646 inclusive.
* If `DeL` is not specified in **Format** as the actual decompressed length, the compressed data is simply read to the end.
* **Checksum** contains an SHA-512 [HMAC](https://en.wikipedia.org/wiki/Hash-based_message_authentication_code), using the binary key and the *compressed* data, rather than a direct SHA-512 hash of the *uncompressed* data.
* **Data** has the following structure:
 1. **Salt:** A sequence of random bits, the same length as the key, used as [salt](https://en.wikipedia.org/wiki/Salt_%28cryptography%29) for the password.
 2. **IV:** the initialization vector (128 bits, the same size as a single encrypted block).
 3. **Encrypted Data:** The encrypted data itself.

The encrypted data contains:
1. **Encrypted Format:** A second **Format** field, containing data which the encoder or the user deem too sensitive to transmit in plaintext, such as the original filename (`Name`). This may include values which are already present in the unencrypted **Format**, as long as this does not result in a contradiction. **Encrypted Format** must be prepended to the data after compression, but before the HMAC is calculated. (The description of the actual encryption must remain in the unencrypted **Format**, or else the decoder won't have any way of knowing that the file is encrypted.)
2. The **Data** field as it exists when unencrypted; the compressed data.

### Text-based passwords
Most end-users are likely to be more interested in using a text-based password than a fixed-length sequence of unintelligible bytes. For the purposes of a DieFledermaus file, the UTF-8 encoding of a textual password must be converted using the [PBKDF2](https://en.wikipedia.org/wiki/PBKDF2) algorithm using a SHA-1 HMAC, with at least 9001 iterations and an output length equal to that of the key. The implementation is equivalent to [that of the .Net framework](https://msdn.microsoft.com/en-us/library/system.security.cryptography.rfc2898derivebytes.aspx).

9001 is chosen because it wastes a hundred or so milliseconds on a modern machine. This number is intended to increase as computers become more powerful; therefore, a DieFledermaus encoder should set this to a higher value as time goes by. At the time of this writing, however, 9001 is good enough, and an encoder should not use anything higher.

The random **Salt** value must be included in the **Data** field even if a binary key is used rather than a text-based password. This is for two reasons: 1. in order to simplify the specification so that it only has a single format, and 2. to avoid revealing anything about the key to an attacker.

Ensuring that the password is [sufficiently strong](https://en.wikipedia.org/wiki/Password_strength) is beyond the scope of this document. That said, an encoder must require that a password should be one byte long at minimum. You've got to have *some* standards.

### Padding
AES is a **block cipher**, which divides the data in to *blocks* of a certain size (128 bits in the case of AES, or 16 bytes). The plaintext must be [padded](https://en.wikipedia.org/wiki/Padding_%28cryptography%29) using the PKCS7 algorithm, and the padding must be added *after* the HMAC is computed. If the length of the compressed plaintext is not a multiple of 16, it must be padded with enough bytes to make it a multiple of 16; the value of each padding byte is equal to the total number of bytes which were added. For example, if the original length is 50 bytes, 14 bytes are added to make a total of 64, and each byte has a value of `0x0e` (14 decimal). If the original value *is* a multiple of 16, then an extra block of 16 bytes must be added to the plaintext, each with a value of `0x10` (16 decimal). The number of padding bytes must not exceed the size of a single block. In short, extra bytes of padding will always be added to the encrypted value, regardless of size.

If the decrypted value has invalid padding (i.e. the last two bytes in the last block are `0x6f02`), this probably means that the key or password is invalid. However, there is a 1 in 256 chance that an incorrect key will transform the last byte in the stream into `0x01`, which is technically valid padding; therefore, the decrypted value must still be compared against the transmitted HMAC.

### Formats
The implementation of AES used by DieFledermaus is equivalent to [that of the .Net framework](https://msdn.microsoft.com/en-us/library/system.security.cryptography.aes.aspx), and operates using **cipher block chaining**. In cipher block chaining, before each block is encrypted, the plaintext version of the current block is XORed with the immediately previous encrypted block; this has the effect that each block has been mixed with the data from *all previous blocks*. The result of this is that 1. multiple identical plaintext blocks will look completely different after encrypting (even in a file which is the same 16 bytes repeated a million times), thus adding to the security; 2. *any* change in the plaintext will also result in a change to every subsequent block; and 3. to decrypt a given block, you need both the current block and the previous block.

Because the first block doesn't have any previous blocks to XOR with, cipher block chaining also includes an [initialization vector](https://en.wikipedia.org/wiki/Initialization_vector) or IV, which is the same size as the block. Without an IV (or with identical IVs), identical encrypted files with identical keys will have identical encrypted binary values, which may lead to a security hole if anyone knows anything about the structure of the plaintext.

An incorrect IV will result in the corruption of the first block of plaintext; a decoder must use the contents of the **IV** field as the initialization vector when decrypting. There is no particular danger to transmitting the original **IV** in plaintext except in specific circumstances which don't really apply to DieFledermaus streams, so an encoder should just set **IV** to the value of the original initialization vector.
