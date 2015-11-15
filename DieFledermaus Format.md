DieFledermaus format (.maus file)
=================================
Version 0.94
------------
* File Extension: ".maus"
* Byte order: little-endian
* Signing form: two's complement

The DieFledermaus file format is simply a [DEFLATE](http://en.wikipedia.org/wiki/DEFLATE)-compressed file, with metadata and a magic number. The name exists solely to be a bilingual pun. Three versions are defined for use: 0.92 (depreciated), 0.93 (depreciated), and 0.94.

Terminology
-----------
* **encoder:** Any application, library, or other software which encodes data to a DieFledermaus stream.
* **decoder:** Any application, library, or other software which restores the data in a DieFledermaus stream to its original form. These may, of course, refer to the same software.

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED",  "MAY", and "OPTIONAL" in this document are to be interpreted as described in [RFC 2119](https://www.ietf.org/rfc/rfc2119.txt).

Structure
---------
Any encoder or decoder must support version 0.94 at minimum. A decoder may support versions 0.92 and/or 0.93, but this is optional. An encoder should not support versions 0.92 or 0.93.

When encoding a file to a DieFledermaus file, the encoder should save it to the same filename but with the extension ".maus" added to the end, unless specified otherwise by the user.

A DieFledermaus stream contains the following fields:

1. **Magic Number:** "`mAuS`" (ASCII `6d 41 75 53`, 4 bytes)
2. **Version:** An unsigned 16-bit value containing the version number in fixed-point form; divide the integer value by 100 to get the actual version number, i.e. `5d 00` (hex) = integer `93` (decimal) = version 0.93.
3. **Format:** An array of length-prefixed strings describing the format.
3. **Compressed Length:** A signed 64-bit integer containing the number of bytes in the DEFLATE stream (that is, the length of the stream *after* compression).
4. **Decompressed Length:** A signed 64-bit integer containing the number of bytes in the stream *before* compression. If the DEFLATE stream decodes to a length greater than this value, the extra data is discarded.
5. **Checksum:** A SHA-512 hash of the decompressed value.
6. **Data:** The DEFLATE-compressed data itself.

### Format
The value of **Format** is an array of strings. The field starts with a single unsigned byte specifying the number of elements in the array; each element in the array is is a string of bytes, each prefixed with an unsigned byte specifying the length, defining the format and how the file is stored. The elements are usually UTF-8 text.

Each element in the array must contain at least one byte. If the length-byte prefix for an element has a value of `00`, the length is 256.

If no element in **Format** specifies the compression format, the file is DEFLATE-compressed.

The following values are supported for strings in the array:
* `Name` (9 bytes) - Indicates that the compressed file has a specified filename. The next element in the array must be used as the filename. Filenames cannot use forward-slashes (`/`, hex `2f`), non-whitespace control characters (non-whitespace characters between `00` and `1f` inclusive or between `7f` and `9f` inclusive), or invalid surrogate characters. Filenames must contain at least one non-whitespace character, and cannot be the "current directory" identifer "." (a single period) or "parent directory" identifier ".." (two periods). If no filename is specified, the decoder should assume that the filename is the same as the DieFledermaus file without the ".maus" extension.
* `KName` (5 bytes) - Indicates that the compressed file has a specified filename, and that the filename is encrypted. The filename is concatenated to the beginning of the compressed data to encrypt, and is used to compute the HMAC value. This is only valid if the file itself is encrypted.
* `NC` (2 bytes) or `NK` (2 bytes) - The contents of the file are not compressed.
* `DEF` (3 bytes) - The contents of the file are DEFLATE-compressed.
* `AES` (3 bytes) - The file is AES-encrypted. To indicate the key length, the next element in the array must be either the three-byte string "128" (that is, a string containing the ASCII characters "1" (`0x31`), "2" (`0x32`), and "8" (`0x38`)), "192", or "256"; or a 16-bit integer (2 bytes) in little-endian order equal to 128, 192, or 256.

If a decoder encounters contradictory values (i.e. both `NC` and `DEF`), it should stop attempting to decode the file, rather than trying to guess what to use, and should clearly inform the user of this error. If a decoder encounters redundant values (i.e. both `NC` and `NK`), the duplicates should be ignored. A decoder must not attempt to decode an archive if it finds any unexpected or unknown values in the **Format** field; that doesn't make sense. It should, however, attempt to decode any *known* format, regardless of the file's version number.

#### Version 0.93
In version 0.93, **Format** was an unsigned 64-bit value. Only the bits 8 and 9 (in LSB-0 order) were used, and four values were defined:
 - `0x000` - DEFLATE compressed, no encryption.
 - `0x100` - DEFLATE compressed, AES-256 encryption.
 - `0x200` - DEFLATE compressed, AES-128 encryption.
 - `0x300` - DEFLATE compressed, AES-192 encryption.

Version 0.92 did not have the **Format** field, and only supported DEFLATE compression with no encryption.

Encryption
----------
Starting with version 0.93, DieFledermaus supports [AES encryption](http://en.wikipedia.org/wiki/Advanced_Encryption_Standard), with 256, 192, and 128-bit keys. Both text-based passwords and raw binary keys are supported. An encoder may use only text-based passwords without any options for setting binary keys, but a decoder must allow both passwords and binary keys.

An encoder should use 256-bit keys, as they are the most secure. A decoder must be able to decode all key sizes, of course.

### Changes to the format
When a DieFledermaus archive is encrypted, the following DieFledermaus fields behave slightly differently:
* **Decompressed Length** contains the number of PBKDF2 cycles, minus 9001. The number of cycles must be between 9001 and 2147483647 inclusive; therefore, the "Decompressed Length" field must have a value between 0 and 2147474646 inclusive. Since no uncompressed length is specified, the DEFLATE data is simply read to the end.
* **Checksum** contains an SHA-512 [HMAC](https://en.wikipedia.org/wiki/Hash-based_message_authentication_code) of the binary key used and the (compressed) plaintext.
* **Data** has the following structure:
 1. **Salt:** A sequence of random bits, the same length as the key, used as [salt](https://en.wikipedia.org/wiki/Salt_%28cryptography%29) for the password.
 2. **IV:** the initialization vector (128 bits, the same size as a single encrypted block).
 3. **Encrypted Data:** The encrypted data itself.

### Text-based passwords
Most end-users are likely to be more interested in using a text-based password than a fixed-length sequence of unintelligible bytes. (Ensuring that the password is [sufficiently strong](https://en.wikipedia.org/wiki/Password_strength) is beyond the scope of this document.) For the purposes of a BSON pack file, the UTF-8 encoding of a textual password must be converted using the [PBKDF2](https://en.wikipedia.org/wiki/PBKDF2) algorithm using a SHA-1 HMAC, with at least 9001 interations and an output length equal to that of the key. The implementation is equivalent to [that of the .Net framework](https://msdn.microsoft.com/en-us/library/system.security.cryptography.rfc2898derivebytes.aspx).

9001 is chosen because it wastes a hundred or so milliseconds on a modern machine. This number is intended to increase as computers become more powerful; therefore, a DieFledermaus encoder should set this to a higher value as time goes by. At the time of this writing, however, 9001 is good enough, and an encoder should not use anything higher.

The random **Salt** value must be included in the **Data** field even if a binary key is used rather than a text-based password. This is for two reasons: 1. in order to simplify the specification so that it only has a single format, and 2. to avoid revealing anything about the key to an attacker.

### Padding
AES is a **block cipher**, which divides the data in to *blocks* of a certain size (128 bits in the case of AES, or 16 bytes). The plaintext must be [padded](https://en.wikipedia.org/wiki/Padding_%28cryptography%29) using the PKCS7 algorithm; the padding must be added *after* the HMAC is computed. If the length of the compressed plaintext is not a multiple of 16, it must be padded with enough bytes to make it a multiple of 16; the value of each padding byte is equal to the total number of bytes which were added. For example, if the original length is 50 bytes, 14 bytes are added to make a total of 64, and each byte has a value of `0x0e` (14 decimal). If the original value *is* a multiple of 16, then an extra block of 16 bytes must be added to the plaintext, each with a value of `0x10` (16 decimal). In short, extra bytes of padding will always be added to the encrypted value, regardless of size.

If the decrypted value has invalid padding (i.e. the last two bytes in the last block are `0x0304`), this probably means that the key or password is invalid. However, there is a 1 in 256 chance that the last byte will be `0x01`, which is technically valid padding; therefore, the decrypted value must still be compared against the transmitted HMAC.

### Formats
The implementation of AES used by DieFledermaus is equivalent to [that of the .Net framework](https://msdn.microsoft.com/en-us/library/system.security.cryptography.aes.aspx), and operates using **cipher block chaining**. In cipher block chaining, before each block is encrypted, the plaintext version of the current block is XORed with the immediately previous encrypted block; this has the effect that each block has been mixed with the data from *all previous blocks*. The result of this is that 1. multiple identical plaintext blocks will look completely different after encrypting (even in a file which is the same 16 bytes repeated a million times), thus adding to the security; 2. *any* change in the plaintext will also result in a change to every subsequent block, and 3. to decrypt a given block, you need both the current block and the previous block.

Because the first block doesn't have any previous blocks to XOR with, cipher block chaining also includes an [initialization vector](https://en.wikipedia.org/wiki/Initialization_vector) or IV, which is the same size as the block. Without an IV (or with identical IVs), identical encrypted files with identical keys will have identical encrypted binary values, which may lead to a security hole if anyone knows anything about the structure of the unencrypted file.

A decoder must use the **IV** field as the IV when decrypting. There are two ways for an encoder to set the **IV** field. The first is to simply transmit the same IV which was used to encrypt the file. An encoder should use the second method, however, because there exist ways of breaking the encryption which rely on the knowledge of the original IV. The second method is as follows.

When decrypting in cipher block chaining mode, decrypting a single block requires only the current block and the immediatelly previous block, and decrypting the first block requires only the current block and the IV; this means that an incorrect IV will *only corrupt the very first block*, and all others will be properly decrypted. An encoder should take advantage of this property using an **explicit initialization vector**: instead of transmitting the IV itself, an encoder should create a block containing random bytes, and (after the HMAC is computed) append this to the beginning of the data to encrypt. This first block takes the place of the **IV** field, and the rest of the encrypted data is used in the **Encrypted Data** field. It is structurally identical to the first method, but more secure.
