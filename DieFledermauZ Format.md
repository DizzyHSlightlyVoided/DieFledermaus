DieFledermauZ (DieFledermaus Zip) format (.mauz file)
=====================================================
Version 0.99
------------
* Extension: ".mauz" or ".maus"
* Byte order: little-endian
* Signing form: two's complement

An archive format, with the purpose of containing multiple files, based on the DieFledermaus single-file-compression format. Like the latter, the name exists solely to be a bilingual pun.

This document uses terminology found in [DieFledermaus Format.md](DieFledermaus Format.md), and assumes familiarity with the DieFledermaus file structure.

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED",  "MAY", and "OPTIONAL" in this document are to be interpreted as described in [RFC 2119](https://www.ietf.org/rfc/rfc2119.txt).

Structure
---------
The structure of a DieFledermauZ file is as follows:

* **Magic Number:** `mAuZ` (`6d 41 75 5a`)
* **Version:** An unsigned 16-bit value containing the version number in fixed-point form. As with DieFledermaus, divide the integer value by 100 to get the actual version number, i.e. `00 63` (hex) = integer `99` (decimal) = version 0.99.
* **Total Size:** A signed 64-bit integer, indicating the total size of the current file in bytes, starting from the `m` in `mAuZ`.
* **Options:** A collection of values signifying options for the archive, with the same form and structure as the **Format** field in a DieFledermaus file.
* **Entry Count:** A signed 64-bit integer, indicating the number of entries in the archive.
* **Entry List:** Contains every entry in the archive.
* **Offset List:** A list of the locations of each element in **Entry List** within the file.
* **Metaoffset:** Indicates the location of the beginning of **Offset List**.

### Options
The following elements are specified for the **Options** field:
* `Kom` - *1 parameter.* Indicates a comment on the DieFledermauZ archive. Same as in DieFledermaus.
* `AES` - *1 parameter.* Indicates that the file is encrypted using the AES algorithm. Same parameter format as in DieFledermaus; must be in plaintext. See below for further information.
* `Twofish` - *1 parameter.* Indicates that the file is encrypted using the Twofish algorithm. Same parameter format as in DieFledermaus; must be in plaintext.
* `Twofish` - *1 parameter.* Indicates that the file is encrypted using the Threefish algorithm. Same parameter format as in DieFledermaus; must be in plaintext.
* `Hash` - *1 parameter.* Indicates the specified hash function for the archive. Must not be used unless the archive is encrypted. Same parameter format as in DieFledermaus; must be in plaintext.

Entry List
----------
The **Entry List** is prefixed with the **Entry List Prefix**, the string "`\x03`DAT" (`03 44 41 54`). The number of elements in the list must be equal to **Entry Count**. Each element in **Entry List** has the following structure:
* **Entry Prefix:** The string "`\x03`dat" (`03 64 61 74`)
* **Entry ID:** A signed 64-bit integer, used to uniquely identify the entry when the filename is encrypted.
* **Entry Filename:** An 8-bit length-prefixed UTF-8 string, in the form of the elements in **Format** and **Options**, specifying the full path of the entry within the DieFledermauZ archive's file structure.
* **Entry:** An entire DieFledermaus file, with a slightly modified format:
 - The `Name` value in the **Entry**'s **Format** is mandatory and must have the same value as the **Entry Filename**, *unless* the filename has been encrypted.
 - `Name` now permits the use of forward-slashes in order to specify directory separators.

The filename must not contain leading and trailing forward-slashes; the individual path-elements must follow the same rules as `Name` as a whole (no zero-length filenames, no elements named "..", etc). The entry filename must be unique, and directory names must not overlap with filenames (you cannot have a file named "Foo.dat/Bar/Baz.txt" if there exists a file named "Foo.dat"). Filenames are case-sensitive, and have a maximum length of 256 UTF-8 bytes.

If a decoder encounters invalid or contradictory file paths in *unencrypted* entries, it should treat the entire archive as invalid. If a decoder encounters invalid or contradictory file paths in entries with *encrypted filenames*, it should treat only the individual entry as invalid.

### Entry ID
The **Entry ID** must be unique, sequential, and start at 0. That is, if there are four entries, they must collectively have the IDs 0, 1, 2, and 3. However, they are not required to be in any specific order. (An encoder should assign them the same order that they are written into the file, but this is because that's just the easier way to do it.)

### Empty directories
A DieFledermauZ file may contain empty directories. They are specified by a filename with a trailing forward-slash and a maximum length of 255 UTF-8 bytes (*including* the forward-slash), no compression (`NK` in **Format**), and the uncompressed data must contain a single byte with a value of `/`. They must not be encrypted except for the purpose of encrypting the filename (see below), and must not include a created or modified time.

Empty directories must be genuinely empty; if an archive contains a file named "Foo/Bar/Baz.txt", it must not also specify an empty directory named "Foo/", because "Foo/" is not empty.

Offset List
-----------
The **Offset List** is prefixed with the **Offset List Prefix**, the string "`\x03`VER" (`03 56 45 52`).

Each element in **Offset List** has the following structure:
* **Offset Prefix:** The string "`\x03`ver" (`03 76 65 72`).
* **Entry ID:** The same value as the corresponding entry in **Entry List**.
* **Entry Filename:** The same value as the corresponding element in **Entry List**.
* **Offset:** The offset of the first byte in the **Entry Prefix** of the corresponding element in **Entry List** from the `m` in `mAuZ` (where the position of `m` = 0).

The **Offset List** is not required to be in the same order as the **Entry List**, as long as every element in **Entry List** has a corresponding element in **Offset List** and vice versa.

The **Metaoffset** field is a signed 64-bit signed integer specifying the offset of the first byte in the Offset List Prefix from the `m` in `mAuZ` (where the position of `m` = 0).

Encryption
----------
There are two ways to deal with encryption. One is to encrypt each entry individually, using the usual format for DieFledermaus streams.

The other way is to encrypt the entire archive. This is indicated by giving **Options** the `AES`, `Twofish`, or `Threefish` options from the DieFledermaus's **Format**. As with DieFledermaus, an encoder should use a PBKDF2-encoded password, but a binary key may also be used. Everything after **Options** is encrypted, and the following plaintext fields are inserted between **Options** and the encrypted values:
* **PBKDF2 Value:** Same as that of a DieFledermaus stream. Uses an HMAC with the specified hash function.
* **HMAC:** An [HMAC](https://en.wikipedia.org/wiki/Hash-based_message_authentication_code) of the plaintext content, including any encrypted values, using the specified hash function.
* **Salt:** The [salt](https://en.wikipedia.org/wiki/Salt_%28cryptography%29) for the password, with a length equal to that of the key.
* **IV:** The initialization vector.
* **Encrypted Data:** The encrypted data.

The encrypted data contains everything from the **Entry Count** to the **Metaoffset**, inclusive. It also contains an **Encrypted Options** field at the beginning, for options which are considered too sensitive to transmit in plaintext. The offsets in the **Offset List** will instead refer to the offset from the beginning of the encrypted data (the first-transmitted byte in the **Encrypted Options**). Trust me, it's easier to validate that way.

If the entire archive is encrypted, individual entries may still individually use their own encryption.

If an encoder is developed with the intention of following the familiar behavior of software such as 7-Zip, it should only allow *either* the encryption of every file with the same key, *or* the encryption of the entire archive without encrypting the individual files. A decoder must behave sensibly regardless of what an arbitrary (valid) archive does, however.

### Ensuring security with individual encryption
If multiple entries are individually encrypted, an encoder should make every effort to ensure that each file's **Salt** and **IV** values are unique, especially when they have the same key or password. That said, the minimum key size and the IV are each 128 bits, so if they are generated by a properly random or pseudorandom number generator, the odds of a collision are 1 in 2<sup>256</sup>, or 1 in 1.159&times;10<sup>77</sup> (that's a 77-digit number), so the danger isn't *too* big.

### Encrypted Filenames
If the filename is encrypted on an individual basis, the **Entry Filename** in both **Entry List** and **Offset List** must be stated as the string "//V" (`2f 2f 56`) followed by a textual representation of **Entry ID** using ASCII digits and no commas, leading zeroes, or separators (i.e. "//V69105", `2f 2f 56 36 39 31 30 35`). When listing files, a decoder should display something more sensible than this, i.e. "(Encrypted Entry at index 69105)".

Signature Manitest
------------------
For further security, an end user may specify that an archive will include a **file manifest**, a file named "/Manifest.dat" (`2f 4d 61 6e 69 66 65 73 74 2e 64 61 74`), which bypasses the usual filename validation and which must be signed, but which is otherwise just like any other file entry. The purpose of this is to protect the validity of the archive itself; without a signature for the entire archive, an attacker might transmit previously-signed valid entries and try to pass them off collectively as a valid archive. The Entry ID of the file manifest must be the highest in the file (i.e. the same as the number of non-manifest files in the archive), and an encoder should always add it to the end of the archive.

The structure of the signature manifest is as follows:

* **Header:** The magic number "`\x03`SIG" (`03 53 49 47`).
* **Signature Count:** A signed 64-bit integer indicating the number of files in the manifest.
* **Signature List:** A list of **manifest entries** with the following format:
 * **Signature Header:** The string "`\x03`sig" (`03 73 69 67`).
 * The **Entry ID** as it occurs in **Entry List** and **Offset List**.
 * The **Entry Filename** as it occurs in **Entry List** and **Offset List**.
 * A 16-bit length-prefixed string containing the contents of the file's **Checksum** field: the hash of the file's uncompressed data, or its HMAC if the file is encrypted.

The manifest must include every file listed in the archive *except* the manifest itself. It must not be compressed, encrypted (apart from encrypting the entire archive), or contain any other entries in its **Format** except the file path.

A decoder which detects an invalid manifest file must treat the archive itself as invalid, for consistency with the way the decoder should behave towards any other invalid format values, even though the manifest is merely "a file within the archive" in the strictest technical sense.

