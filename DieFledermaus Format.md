DieFledermaus format (.maus file)
=================================
Version 0.99
------------
* File Extension: ".maus"
* Byte order: little-endian
* Signing form: two's complement

The DieFledermaus file format is simply a [DEFLATE](https://en.wikipedia.org/wiki/DEFLATE)- or [LZMA](https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Markov_chain_algorithm)-compressed file, with metadata and a magic number. The name exists solely to be a bilingual pun.

Terminology
-----------
* **decompressed file** or **decompressed data:** The file as it originally existed before compression.
* **compressed file** or **compressed data:** The file as it exists after compression, but before encryption. This is referred to as such even if the compression-mode is *none*.
* **encoder:** Any application, library, or other software which encodes data to a DieFledermaus stream.
* **decoder:** Any application, library, or other software which restores the data in a DieFledermaus stream to its original form.
* **re-encoder:** Any software which functions as both an encoder and a decoder.
* **length-prefixed string:** In the DieFledermaus format, a length-prefixed string is a sequence of bytes, usually UTF-8 text, which is prefixed by the *length value*, an 8-bit or 16-bit unsigned integer indicating the length of the string (not including the length value itself). If the length value is 0, the actual length of the string is 256 for 8-bit length values and 65536 for 16-bit length values.
* **the specified hash function:** A DieFledermaus file must use one of the following cryptographic hash functions: [SHA-256, SHA-512](https://en.wikipedia.org/wiki/SHA-2), [SHA-3/256, or SHA-3/512](https://en.wikipedia.org/wiki/SHA-3). "The specified hash function" refers to whichever function the file is currently using.

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED",  "MAY", and "OPTIONAL" in this document are to be interpreted as described in [RFC 2119](https://www.ietf.org/rfc/rfc2119.txt).

Structure
---------
Any encoder or decoder must support version 0.98 at minimum. A decoder must be able to support any non-depreciated version, but an encoder may only support a single version. A re-encoder should encode using the highest version understood by the decoder.

When encoding a file to a DieFledermaus archive, the filename of the DieFledermaus file should be the same as the file to encode but with the extension ".maus" added to the end, unless a specific filename is requested by the user.

A DieFledermaus stream contains the following fields:

* **Magic Number:** "`mAuS`" (`6d 41 75 53`)
* **Version:** An unsigned 16-bit value containing the version number in fixed-point form; divide the integer value by 100 to get the actual version number, i.e. `63 00` (hex) = integer `99` (decimal) = version 0.98.
* **Format:** A collection of length-prefixed strings describing the format.
* **Compressed Length:** A signed 64-bit integer containing the number of bytes in the compressed data.
* **Decompressed Length:** A signed 64-bit integer containing the number of bytes in the uncompressed data. If the compressed data stream decodes to a length greater than this value, the extra data is discarded. The minimum length of the decompressed data must be 1 byte.
* **Checksum:** A hash of the decompressed value using the specified hash function.
* **Data:** The compressed data itself.

### Format
**Format** is a collection of 16-bit length-prefixed strings, used to specify information about the format of the encoded data. The field starts with the **Format Length**, an unsigned 16-bit integer specifying the number of elements in the array; unlike the length-prefixed strings themselves, a 0-value in the **Format Length** means that there really are zero elements. Format elements are case-sensitive.

Each element in **Format** has the following structure:
* **Key:** A length-prefixed string of UTF-8 characters, indicating the name of the format element.
* **Version:** A 16-bit unsigned integer indicating the version number.
* **Parameter Count:** A 16-bit unsignd integer indicating the number of *parameters.* Some elements require further information; for example, `Ver` indicates that the archive is encrypted, but does not specify what type of encryption is used or the size of the key.
* Zero or more **Parameters**, each one consisting of a length-prefixed string of binary data, though some of them might contain text.

Some elements must be in the plaintext, even if the file is encrypted, because they contain vital information about the encryption itself and/or the structure of the file. An encoder may include them in the **Encrypted Format**, but only if they are also included in the plaintext **Format**. Other elements are potentially sensitive enough that, if the file is encrypted, they should only be in the **Encrypted Format**, not the plaintext **Format**.

If no element in **Format** specifies the compression format, the decoder must use the DEFLATE algorithm.

The following values are defined for the default implementation:
* `Name` - *version 1, one parameter.* Indicates that the compressed file has a filename, specified in the parameter. Filenames must not contain forward-slashes (`/`, hex `2f`), non-whitespace control characters (non-whitespace characters between `00` and `1f` inclusive or between `7f` and `9f` inclusive), or invalid surrogate characters. Filenames must contain at least one non-whitespace character, and cannot be the "current directory" identifier "." (a single period) or "parent directory" identifier ".." (two periods). If no filename is specified, the decoder should assume that the filename is the same as the DieFledermaus file without the ".maus" extension. A filename must be less than or equal to 256 UTF-8 bytes.
* `NK` - *version 1, no parameters.* **N**icht **K**omprimiert ("not compressed"). Indicates that the file is not compressed.
* `DEF` - *version 1, no parameters.* Indicates that the file is compressed using the [DEFLATE](http://en.wikipedia.org/wiki/DEFLATE) algorithm.
* `LZMA` - *version 1, no parameters.* Indicates that the file is compressed using [LZMA](https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Markov_chain_algorithm). Like DEFLATE, LZMA is based on the [LZ77 algorithm](https://en.wikipedia.org/wiki/LZ77_and_LZ78). The format of the LZMA stream is the 5-byte header, followed by every block in the stream. Due to the limitations of the .Net Framework implementation of LZMA, the dictionary size must be less than or equal to 64 megabytes.
* `Ver` - *version 1, 2 parameters.* **Ver**schlüsselte ("encrypted"). The file is encrypted. The first parameter specifies the type of encryption, and must be one of the strings "[AES](http://en.wikipedia.org/wiki/Advanced_Encryption_Standard)", "[Twofish](https://en.wikipedia.org/wiki/Twofish)", and "[Threefish](https://en.wikipedia.org/wiki/Threefish)" (case-sensitive). The second parameter indicates the number of bits in the key, as a 16-bit unsigned value; it must be one of 128, 192, or 256 for AES and Twofish, and 256, 512, or 1024 for Threefish.
* `DeL` - *version 1, one parameter.* **De**compressed **L**ength, or **De**komprimierte **L**änge. The parameter is a signed 64-bit integer containing the number of bytes in the uncompressed data. If the archive is not encrypted, this value must be equal to **Decompressed Length**. This value should be included in the **Encrypted Format** array when the archive is encrypted.
* `Ers` - *version 1, one parameter.* **Ers**tellt ("created"). Indicates when the file to compress was originally created. The time is in UTC form, and is stored as a 64-bit integer containing the number of [.Net Framework "ticks" (defined as 100 nanoseconds) since 0001-01-01T00:00:00Z](https://msdn.microsoft.com/en-us/library/system.datetime.ticks.aspx), excluding leap seconds. The minimum value is 0 (or 0001-01-01T00:00:00Z), and the maximum value is 9999-12-31T23:59:59.9999999Z.
* `Mod` - *version 1, one parameter.* **Mod**ified, or **Mod**ifiziert. Indicates when the file to compress was last modified. Same format as `Ers`.
* `Kom` - *version 1, one parameter.* **Kom**mentar ("comment"). A textual comment. This is not directly used or processed by any decoder; it can either be treated as a raw binary value, or as UTF-8 string of text (or indeed any other text encoding).
* `Hash` - *1 parameter.* Indicates the specified hash function. Valid values of the parameter are:
 - `SHA224`
 - `SHA256` (the default if nothing is specified)
 - `SHA384`
 - `SHA512`
 - `SHA3/224`
 - `SHA3/256`
 - `SHA3/384`
 - `SHA3/512`
* `RSAsig` - *version 1, one or two parameters.* "**RSA sig**niert", or "**RSA sig**ned". The stream is digitally signed with an RSA private key, using the result of the specified hash function on the uncompressed data. The signature may be verified using the corresponding RSA public key. The object ID of the specified hash function is included using [DER encoding](https://en.wikipedia.org/wiki/X.690), and [OAEP padding](https://en.wikipedia.org/wiki/Optimal_asymmetric_encryption_padding) is then also used. The first parameter contains the encrypted value; the optional second parameter contains a value (string or binary) which identifies the RSA public key an encoder should use to verify `RSAsig`. Should be encrypted.
* `DSAsig` - *version 1, one or two parameters.* Same as `RSAsig`, but using the [DSA](https://en.wikipedia.org/wiki/Digital_Signature_Algorithm) algorithm. The *r,s* signature values are transmitted using [DER encoding](https://en.wikipedia.org/wiki/X.690). The message value *k* is generated deterministically using an HMAC of the specified hash function, as described in [RFC 6979](https://tools.ietf.org/html/rfc6979). Should be encrypted.
* `ECDSAsig` - *version 1, one or two parameters.* Same as `DSAsig`, but using the [ECDSA](https://en.wikipedia.org/wiki/Elliptic_Curve_Digital_Signature_Algorithm) algorithm.

If a decoder encounters contradictory values (i.e. both `LZMA` and `DEF`), it should stop attempting to decode the file rather than trying to guess what to use, and should inform the user of this error. If a decoder encounters redundant values (i.e. two `Name` items which are each followed by the same filename), the duplicates should be ignored.

A decoder must not attempt to decode an archive if it finds any unexpected or unknown values in the **Format** field, or unexpected or unknown version numbers; that doesn't make sense. It should, however, attempt to decode any *known* format, regardless of the file's version number.

Encryption
----------
DieFledermaus supports encryption using the [AES algorithm](http://en.wikipedia.org/wiki/Advanced_Encryption_Standard), with 256-, 192-, and 128-bit keys; the [Twofish algorithm](http://en.wikipedia.org/wiki/Twofish), with 256-, 192-, and 128-bit keys; and the [Threefish algorithm](http://en.wikipedia.org/wiki/Threefish), with 256-, 512-, and 1024-bit keys. An encoder should derive the key from a UTF-8 text-based password. A decoder may allow setting the key directly.

An encoder should use the maximum key size for the specified algorithm, as they are the most secure. A decoder must be able to decode all key sizes, of course.

### Changes to the format
When a DieFledermaus archive is encrypted, the following DieFledermaus fields behave slightly differently:
* **Decompressed Length** is replaced with the **PBKDF2 Value**, which is still a signed 64-bit integer to make the structure more straightforward. This value is the number of [PBKDF2](https://en.wikipedia.org/wiki/PBKDF2) cycles, minus 9001. The number of cycles must be between 9001 and 2147483647 inclusive; therefore, the field must have a value between 0 and 2147474646 inclusive.
* If `DeL` is not specified in **Format** as the actual decompressed length, the compressed data is simply read to the end.
* **Checksum** contains an [HMAC](https://en.wikipedia.org/wiki/Hash-based_message_authentication_code), using the specified hash function, the binary key derived from the password, and the *compressed* data, rather than a direct hash of the *uncompressed* data.
* **Data** has the following structure:
 - **Salt:** A sequence of random bits, the same length as the key, used as [salt](https://en.wikipedia.org/wiki/Salt_%28cryptography%29) for the password.
 - **IV:** the initialization vector (the same size as a single encrypted block).
 - **Encrypted Data:** The encrypted data itself.

The encrypted data contains:
1. **Encrypted Format:** A second **Format** field, containing data which the encoder or the user deem too sensitive to transmit in plaintext, such as the original filename. This may include values which are already present in the unencrypted **Format**, as long as this does not result in a contradiction. **Encrypted Format** must be prepended to the data after compression, but before the HMAC is calculated. (The description of the actual encryption must remain in the unencrypted **Format**, or else the decoder won't have any way of knowing that the file is encrypted.)
2. The **Data** field as it exists when unencrypted; the compressed data.

### Text-based passwords
If a password is used instead of directly using a binary key, the canonical form of converting a password to a key is as follows.

The UTF-8 encoding of a text-based password must be converted using the [PBKDF2](https://en.wikipedia.org/wiki/PBKDF2) algorithm, using an HMAC with specified hash function, with at least 9001 iterations, and with an output length equal to that of the key. The **Salt** field is used as [cryptographic salt](http://en.wikipedia.org/wiki/Salt_%28cryptography%29) for the password generation.

9001 is chosen because it wastes a hundred or so milliseconds on a modern machine. This number is intended to increase as computers become more powerful; therefore, a DieFledermaus encoder should set this to a higher value as time goes by. At the time of this writing, however, 9001 is good enough, and an encoder should not use anything higher.

Ensuring that the password is [sufficiently strong](https://en.wikipedia.org/wiki/Password_strength) is beyond the scope of this document. That said, an encoder must require a minimum length of 1 byte; you've got to have *some* standards.

The **Salt** field must be included even if the file does not use a text-based password, both to simplify the format and specification and to prevent any information about the key from being revealed to an attacker.

### Padding
AES, Twofish, and Threefish are [block ciphers](https://en.wikipedia.org/wiki/Block_cipher), which means that they divide the data in to *blocks* of a certain size (128 bits in the case of AES and Twofish, or 16 bytes; and with a block size equal to the key size, in the case of Threefish, equal to 32, 64, and 128 bytes). The plaintext must be [padded](https://en.wikipedia.org/wiki/Padding_%28cryptography%29) using the PKCS7 algorithm, and the padding must be added *after* the HMAC is computed. If the length of the compressed plaintext is not a multiple of the block size, it must be padded with enough bytes to make it a multiple of the block size; the value of each padding byte is equal to the total number of bytes which were added.

For example, in the case of AES: the original length is 50 bytes. 14 bytes are added to make a total of 64, and each byte has a value of `0x0e` (14 decimal).

If the original value *is* a multiple of 16, then an extra block of 16 bytes must be added to the plaintext, each with a value of `0x10` (16 decimal). In short, extra bytes of padding must always be added to the encrypted value. The number of padding bytes must not exceed the size of a single block.

If the decrypted value has invalid padding (i.e. the last two bytes in the last block are `6f 02`), this probably means that the key or password is invalid. However, there is effectively a 1 in 256 chance that an incorrect key will transform the last byte in the stream into `0x01`, which is technically valid padding; therefore, the decrypted value must still be compared against the transmitted HMAC after the padding is removed.

### Formats
The DieFledermaus format using **cipher block chaining**. In cipher block chaining, before each block is encrypted, the plaintext version of the current block is XORed with the immediately previous encrypted block; this has the effect that each block has been mixed with the data from *all previous blocks*. The result of this is that 1. multiple identical plaintext blocks will look completely different after encrypting (even in a file which is the same 16 bytes repeated a million times), thus adding to the security; 2. *any* change in the plaintext will also result in a change to every subsequent block; and 3. to decrypt a given block, you need both the current block and the previous block.

Because the first block doesn't have any previous blocks to XOR with, cipher block chaining also includes an [initialization vector](https://en.wikipedia.org/wiki/Initialization_vector) or IV, which is the same size as a single block. Without an IV (or with identical IVs), identical encrypted files with identical keys will have identical encrypted binary values, which may lead to a security hole if anyone knows anything about the structure of the plaintext.

An incorrect IV will result in the corruption of the first block of plaintext; a decoder must use the contents of the **IV** field as the initialization vector when decrypting. There is no particular danger to transmitting the original **IV** in plaintext except in specific circumstances which don't really apply to DieFledermaus streams, so an encoder should just set **IV** to the value of the original initialization vector.
