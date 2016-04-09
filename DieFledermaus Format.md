DieFledermaus format (.maus file)
=================================
Version 1.01
------------
* File Extension: ".maus"
* Byte order: little-endian
* Signing form: two's complement

The DieFledermaus file format is simply a [DEFLATE](https://en.wikipedia.org/wiki/DEFLATE)- or [LZMA](https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Markov_chain_algorithm)-compressed file, with metadata and a magic number. It exists mostly to be a bilingual pun; however, it is also a fully-functioning archive format.

Terminology
-----------
* **decompressed file** or **decompressed data:** The file as it originally existed before compression.
* **compressed file** or **compressed data:** The file as it exists after compression, but before encryption. This is referred to as such even if the compression-mode is *none*.
* **encoder:** Any application, library, or other software which encodes data to a DieFledermaus stream.
* **decoder:** Any application, library, or other software which restores the data in a DieFledermaus file to its original form.
* **re-encoder:** Any software which functions as both an encoder and a decoder.
* **length-prefixed string:** In the DieFledermaus format, a length-prefixed string is a sequence of bytes, usually UTF-8 text, which is prefixed by the *length value*, an 8-bit or 16-bit unsigned integer indicating the length of the string (not including the length value itself). If the length value is 0, the actual length of the string is 256 for 8-bit length values and 65536 for 16-bit length values.
* **the specified hash function:** A DieFledermaus file must use one of the following cryptographic hash functions for various purposes: [SHA-224, SHA-256, SHA-384, SHA-512](https://en.wikipedia.org/wiki/SHA-2), [SHA-3/224, SHA-3/256, SHA-3/384, or SHA-3/512](https://en.wikipedia.org/wiki/SHA-3). "The specified hash function" refers to whichever function the file is currently using. Within a DieFledermaus file, the same hash function is used in all contexts.

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED",  "MAY", and "OPTIONAL" in this document are to be interpreted as described in [RFC 2119](https://www.ietf.org/rfc/rfc2119.txt).

Structure
---------
An encoder or decoder must support version 1.01 at minimum. A decoder must be able to support any non-depreciated version, but an encoder may only support a single version. A re-encoder should encode using the highest version understood by the decoder.

When encoding a file to a DieFledermaus archive, the filename of the DieFledermaus file should be the same as the file to encode but with the extension ".maus" added to the end, unless specified otherwise by the user.

A DieFledermaus stream contains the following fields:

* **Magic Number:** "`mAuS`" (`6d 41 75 53`)
* **Version:** An unsigned 16-bit value containing the version number in fixed-point form; divide the integer value by 100.0 to get the actual version number, i.e. `65 00` (hex) = integer `101` (decimal) = version 1.01.
* **Primary Format:** A collection of options specifying the format.
* **Compressed Length:** A signed 64-bit integer containing the number of bytes in the **Data** field.
* **Decompressed Length:** A signed 64-bit integer containing the number of bytes in the uncompressed data. If the decoder determines that the compressed data decodes to a length greater than this value, it must discard the extra data. This is just for error checking, however; an encoder should set this to the same length as the actual uncompressed length. The minimum length of the decompressed data must be 1 byte.
* **HMAC:** A hash of the **Secondary Format**, **Checksum**, and **Data** fields using the specified hash function.
* **Secondary Format:**  A second collection of options.
* **Checksum:** A hash of the decompressed data using the specified hash function.
* **Data:** The compressed data itself.

### Format
**Primary Format** and **Secondary Format** are used to specify information about the format of the encoded data. The field starts with the **Format Length**, an unsigned 16-bit integer specifying the number of elements in the collection; unlike length-prefixed strings, a 0-value in the **Format Length** means that there really are zero elements. Format elements are case-sensitive.

Each element in **Format** has the following structure:
* **Key:** A length-prefixed string of UTF-8 characters, indicating the name of the format element.
* **Version:** A 16-bit unsigned integer indicating the version number.
* **Parameter Count:** A 16-bit unsigned integer indicating the number of *parameters.* Some elements require further information; for example, `Ver` indicates that the archive is encrypted, but does not specify what type of encryption is used or the size of the key.
* Zero or more **Parameters**, each one consisting of a length-prefixed string of binary data, though some of them might contain text.

The difference between **Primary Format** and **Secondary Format** is chiefly that **Secondary Format** is encrypted along with the rest of the file and is used to compute the **HMAC** field. Some elements must be in the **Primary Format** because they contain functional information about the encryption itself and/or the structure of the file, or contain values derived from the **HMAC** field. An encoder may include them in the **Secondary Format**, but only if they are also included in **Primary Format**. In general, anything which is not required to be in **Primary Format** should be placed in **Secondary Format** for the purpose of security, and for the purpose of ensuring that the **HMAC** field gets a complete picture of the file.

If no element in either **Format** specifies the compression algorithm, the decoder must use the DEFLATE algorithm.

The following values are defined for the default implementation:
* `Name` - *version 1, one parameter.* Indicates that the compressed file has a filename, specified in the parameter. Filenames must not contain forward-slashes (`/`, hex `2f`), non-whitespace control characters (non-whitespace characters between `00` and `1f` inclusive or between `7f` and `9f` inclusive), or invalid surrogate characters. Filenames must contain at least one non-whitespace character, and cannot be the "current directory" identifier "." (a single period) or "parent directory" identifier ".." (two periods). If no filename is specified, the decoder should assume that the filename is the same as the DieFledermaus file without the ".maus" extension. A filename must be less than or equal to 256 UTF-8 bytes.
* `NK` - *version 1, no parameters.* **N**icht **K**omprimiert ("not compressed"). Indicates that the file is not compressed.
* `DEF` - *version 1, no parameters.* Indicates that the file is compressed using the [DEFLATE](http://en.wikipedia.org/wiki/DEFLATE) algorithm.
* `LZMA` - *version 1, no parameters.* Indicates that the file is compressed using [LZMA](https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Markov_chain_algorithm). Like DEFLATE, LZMA is based on the [LZ77 algorithm](https://en.wikipedia.org/wiki/LZ77_and_LZ78). The format of the LZMA stream is the 5-byte header, followed by every block in the stream. Due to the limitations of the .Net Framework implementation of LZMA, the dictionary size must be less than or equal to 64 megabytes.
* `Ver` - *version 1, 2 parameters.* **Ver**schlüsselte ("encrypted"). The file is encrypted. The first parameter specifies the type of encryption, and must be one of the strings "[AES](http://en.wikipedia.org/wiki/Advanced_Encryption_Standard)", "[Twofish](https://en.wikipedia.org/wiki/Twofish)", and "[Threefish](https://en.wikipedia.org/wiki/Threefish)" (case-sensitive). The second parameter indicates the number of bits in the key, as a 16-bit unsigned value; it must be one of 128, 192, or 256 for AES and Twofish, and 256, 512, or 1024 for Threefish. Must be in **Primary Format**.
* `DeL` - *version 1, one parameter.* **De**compressed **L**ength, or **De**komprimierte **L**änge. The parameter is a signed 64-bit integer containing the number of bytes in the uncompressed data. If the archive is not encrypted, this value must be equal to **Decompressed Length**. Should be in **Secondary Format** only.
* `Ers` - *version 1, one parameter.* **Ers**tellt ("created"). Indicates when the file to compress was originally created. The time is in UTC form, and is stored as a 64-bit integer containing the number of [.Net Framework "ticks" (defined as 100 nanoseconds) since 0001-01-01T00:00:00Z](https://msdn.microsoft.com/en-us/library/system.datetime.ticks.aspx), excluding leap seconds. The minimum value is 0 (or 0001-01-01T00:00:00Z), and the maximum value is 9999-12-31T23:59:59.9999999Z.
* `Mod` - *version 1, one parameter.* **Mod**ified, or **Mod**ifiziert. Indicates when the file to compress was last modified. Same format as `Ers`.
* `Kom` - *version 1, one parameter.* **Kom**mentar ("comment"). A textual comment. This is not directly used or processed by any decoder; it can either be treated as a raw binary value, or as UTF-8 string of text (or indeed any other text encoding).
* `Hash` - *1 parameter.* Indicates the specified hash function. Must be in **Primary Format**. Valid values of the parameter are one of the following UTF-8 strings:
 - `SHA224`
 - `SHA256` (the default if nothing is specified)
 - `SHA384`
 - `SHA512`
 - `SHA3/224`
 - `SHA3/256`
 - `SHA3/384`
 - `SHA3/512`
 - `Whirlpool`
* `RSAsig` - *version 1, one or two parameters.* "**RSA sig**niert", or "**RSA sig**ned". The stream is digitally signed with an RSA private key, using the value of the **HMAC** field. The signature may be verified using the corresponding RSA public key. The object ID of the specified hash function is included using [DER encoding](https://en.wikipedia.org/wiki/X.690), and [OAEP padding](https://en.wikipedia.org/wiki/Optimal_asymmetric_encryption_padding) is then also used. The first parameter contains the encrypted value; the optional second parameter contains a value (string or binary) which identifies the RSA public key an encoder should use to verify `RSAsig`. Must be in **Primary Format**.
* `DSAsig` - *version 1, one or two parameters.* Same as `RSAsig`, but using the [DSA](https://en.wikipedia.org/wiki/Digital_Signature_Algorithm) algorithm. The *r,s* signature values are transmitted using [DER encoding](https://en.wikipedia.org/wiki/X.690). The message value *k* should be generated deterministically using an HMAC of the specified hash function, as described in [RFC 6979](https://tools.ietf.org/html/rfc6979). Must be in **Primary Format**.
* `ECDSAsig` - *version 1, one or two parameters.* Same as `DSAsig`, but using the [ECDSA](https://en.wikipedia.org/wiki/Elliptic_Curve_Digital_Signature_Algorithm) algorithm. As with DSA, *k* should be generated deterministically. Must be in **Primary Format**.
* `RSAsch` - *version 1, one parameter.* "**RSA Sch**lüssel" ("RSA key"). The encoder encrypts the binary key using an RSA public key, and is transmitted as the parameter. The decoder then uses the corresponding RSA private key to decrypt the key. Must be in **Primary Format**, and must not be used unless the file is encrypted.

If a decoder encounters contradictory values (i.e. both `LZMA` and `DEF`), it must stop attempting to decode the file rather than trying to guess what to use, and should inform the user of this error. If a decoder encounters redundant values (i.e. two `Name` items which are each followed by the same filename), the duplicates should be ignored.

A decoder must not attempt to decode an archive if it finds any unexpected or unknown values in the **Format** field, or unexpected or unknown version numbers; that doesn't make sense. It should, however, attempt to decode any *known* format, regardless of the file's version number.

Encryption
----------
DieFledermaus supports encryption using the [AES algorithm](http://en.wikipedia.org/wiki/Advanced_Encryption_Standard), with 256-, 192-, and 128-bit keys; the [Twofish algorithm](http://en.wikipedia.org/wiki/Twofish), with 256-, 192-, and 128-bit keys; and the [Threefish algorithm](http://en.wikipedia.org/wiki/Threefish), with 256-, 512-, and 1024-bit keys. An encoder should derive the key from a UTF-8 text-based password. A decoder may allow setting the key directly. The **Secondary Format**, **Checksum**, and **Data** fields are encrypted.

An encoder should use the maximum key size for the specified algorithm, as they are the most secure. A decoder must be able to decode all key sizes, of course.

### Changes to the format
When a DieFledermaus archive is encrypted, the following DieFledermaus fields behave slightly differently:
* **Compressed Length** is replaced with the **Encrypted Length**, equal to the entire length of the encrypted data, not just the compressed data.
* **Decompressed Length** is replaced with the **PBKDF2 Value**, which is still a signed 64-bit integer to make the structure more straightforward. This value is the number of [PBKDF2](https://en.wikipedia.org/wiki/PBKDF2) cycles, minus 9001. The number of cycles must be between 9001 and 2147483647 inclusive; therefore, the field must have a value between 0 and 2147474646 inclusive. At the time of this writing, 9001 is enough for most purposes, so an encoder should just leave this at 0.
 - If `DeL` is not specified in **Secondary Format** as the actual decompressed length, the compressed data is simply read to the end.
* **HMAC** contains an [HMAC](https://en.wikipedia.org/wiki/Hash-based_message_authentication_code) using the specified hash function and the binary key, rather than just a checksum.
* After **HMAC**, the following fields are inserted:
 - **Salt:** A sequence of random bits, the same length as the key, used as [salt](https://en.wikipedia.org/wiki/Salt_%28cryptography%29) for the password.
 - **IV:** the initialization vector (the same size as a single encrypted block).

The encrypted data contains the fields used to derive **HMAC**: **Secondary Format**, **Checksum**, and **Data**.

### Text-based passwords
If a password is used instead of directly using a binary key, the canonical form of converting a password to a key is as follows.

The UTF-8 encoding of a text-based password must be converted using the [PBKDF2](https://en.wikipedia.org/wiki/PBKDF2) algorithm, using an HMAC with specified hash function, with at least 9001 iterations, and with an output length equal to that of the key. The **Salt** field is used as [cryptographic salt](http://en.wikipedia.org/wiki/Salt_%28cryptography%29) for the password generation.

9001 is chosen because it wastes a hundred or so milliseconds on a modern machine. This number is intended to increase as computers become more powerful; therefore, a DieFledermaus encoder should set this to a higher value as time goes by. At the time of this writing, however, 9001 is good enough, and an encoder should not use anything higher.

Ensuring that the password is [sufficiently strong](https://en.wikipedia.org/wiki/Password_strength) is beyond the scope of this document. That said, an encoder must require a minimum length of 1 byte; you've got to have *some* standards.

The **Salt** field must be included even if the file does not use a text-based password, both to simplify the format and specification and to prevent any information about the key from being revealed to an attacker.

### Padding
AES, Twofish, and Threefish are [block ciphers](https://en.wikipedia.org/wiki/Block_cipher), which means that they divide the data in to *blocks* of a certain size (128 bits in the case of AES and Twofish, or 16 bytes; and with a block size equal to the key size, in the case of Threefish, equal to 32, 64, and 128 bytes). The plaintext must be [padded](https://en.wikipedia.org/wiki/Padding_%28cryptography%29) *after* the HMAC is computed, and using the PKCS7 algorithm:

If the length of the compressed plaintext is not a multiple of the block size, it must be padded with enough bytes to make a complete block; the value of each padding byte is equal to the total number of bytes which were added. For example, in the case of AES: if the original length is 50 bytes, 14 bytes are added to make a total of 64, and each padding byte has a value of `0x0e` (14 decimal).

If the original value *is* a multiple of the block size, then an entire extra block of bytes must be added to the plaintext, i.e. with AES and an original length of 48 bytes, you'd add 16 bytes, each with a value of `0x10` (16 decimal). In other words, extra bytes of padding must *always* be added to the encrypted value. The number of padding bytes must not exceed the size of a single block.

If the decrypted value has invalid padding (i.e. the last two bytes in the last block are `6f 02`), this probably means that the key or password is invalid. However, there is effectively a 1 in 256 chance that an incorrect key will transform the last byte in the stream into `0x01`, which is technically valid padding; therefore, the decrypted data must still be compared against the transmitted HMAC after the padding is removed.

### Formats
The DieFledermaus format uses **cipher block chaining**. In cipher block chaining, before each block is encrypted, the plaintext version of the current block is XORed with the immediately previous encrypted block; this has the effect that each block has been mixed with the data from *all previous blocks*. The result of this is that 1. multiple identical plaintext blocks will look completely different after encrypting (even in a file which is the same 16 bytes repeated a million times), thus adding to the security; 2. *any* change in the plaintext will also result in a change to every subsequent block; and 3. to decrypt a given block, you need both the current block and the previous block.

Because the first block doesn't have any previous blocks to XOR with, cipher block chaining also includes an [initialization vector](https://en.wikipedia.org/wiki/Initialization_vector) or IV, which is the same size as a single block. Without an IV (or with identical IVs), identical encrypted files with identical keys will have identical encrypted binary values, which may lead to a security hole if anyone knows anything about the structure of the plaintext.

An incorrect IV will result in the corruption of the first block of plaintext; a decoder must use the contents of the **IV** field as the initialization vector when decrypting. There is no particular danger to transmitting the original **IV** in plaintext except in specific circumstances which don't really apply to DieFledermaus streams, so an encoder should just set **IV** to the value of the original initialization vector.
