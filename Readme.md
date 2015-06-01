DieFledermaus
=============

A C# library for the Die Fledermaus compression algorithm, which is simply:

1. The magic number "`mAuS`" (ASCII `6d 41 75 53`)
2. A signed 64-bit integer in little-endian order, containing the length of field 3
3. A DEFLATE stream
4. An SHA-512 checksum.

The library contains one public type, `DieFledermaus.DieFledermausStream`, which provides the same functionality as the [`System.IO.Compression.DeflateStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.deflatestream.aspx) class.