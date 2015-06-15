DieFledermaus format 
====================
Version 0.91
------------

A C# library for the Die Fledermaus compression algorithm, which is simply the [DEFLATE algorithm](http://en.wikipedia.org/wiki/DEFLATE) with metadata and a magic number. The name exists solely to be a bilingual pun. A Die Fledermaus stream contains:

1. The magic number "`mAuS`" (ASCII `6d 41 75 53`, 4 bytes)
2. A single-precision floating-point number in little-endian order, containing the minimum version number required to read the Die Fledermaus stream.
3. A signed 64-bit integer in little-endian order, containing the number of bytes in the DEFLATE stream (that is, the length of the stream after compression).
4. A signed 64-bit integer in little-endian order, containing the number of bytes in the stream before compression. If the DEFLATE stream 
5. A SHA-512 checksum.
6. The DEFLATE-compressed data itself.

The library contains one public type, `DieFledermaus.DieFledermausStream`, which provides the same functionality as the [`System.IO.Compression.DeflateStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.deflatestream.aspx) or [`System.IO.Compression.GZipStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.gzipstream.aspx) classes. The SHA-512 checksum is computed using the [`System.Security.Cryptography.SHA512Managed`](https://msdn.microsoft.com/en-us/library/system.security.cryptography.sha512managed.aspx) class.
