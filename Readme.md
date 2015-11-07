DieFledermaus format 
====================
Version 0.92
------------

A C# library for the Die Fledermaus compression algorithm, which is simply the [DEFLATE algorithm](http://en.wikipedia.org/wiki/DEFLATE) with metadata and a magic number. The name exists solely to be a bilingual pun. A Die Fledermaus stream contains the following :

1. **Magic Number:** "`mAuS`" (ASCII `6d 41 75 53`, 4 bytes)
2. **Version:** An unsigned 16-bit value in little-endian order, containing the version number in fixed-point form. Essentially, it's an integer equal to 100 times the version number, so `5c 00` (hex) = integer `92` (decimal) = version 0.92.
3. **Compressed Length:** A 64-bit integer in little-endian order, signed in two's complement form, containing the number of bytes in the DEFLATE stream (that is, the length of the stream after compression).
4. **Decompressed Length:** A 64-bit integer in little-endian order, signed in two's complement form, containing the number of bytes in the stream before compression. If the DEFLATE stream decodes to a length greater than this value, the extra data is discarded.
5. **Checksum:** A SHA-512 hash of the decompressed value.
6. **Data:** The DEFLATE-compressed data itself.

The library contains one public type, `DieFledermaus.DieFledermausStream`, which provides the same functionality as the [`System.IO.Compression.DeflateStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.deflatestream.aspx) or [`System.IO.Compression.GZipStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.gzipstream.aspx) classes. The SHA-512 checksum is computed using the [`System.Security.Cryptography.SHA512Managed`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.sha512managed.aspx) class.
