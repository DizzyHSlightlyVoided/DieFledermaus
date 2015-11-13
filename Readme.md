DieFledermaus format (.maus file)
=================================
Version 0.94
------------
The DieFledermaus file format is simply a [DEFLATE](http://en.wikipedia.org/wiki/DEFLATE)-compressed file, with metadata and a magic number. The name exists solely to be a bilingual pun. Three versions are defined for use: 0.92 (depreciated), 0.93 (depreciated), and 0.94. The file format is specified in [DieFledermaus Format.md](DieFledermaus Format.md).

Class Library
=============
The library contains one public type, `DieFledermaus.DieFledermausStream`, which provides much the same functionality as the [`System.IO.Compression.DeflateStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.deflatestream.aspx) or [`System.IO.Compression.GZipStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.gzipstream.aspx) classes. Unlike either of these classes, however, DieFledermausStream must read part of the underlying stream before [`Stream.Read()`](https://msdn.microsoft.com/en-us/library/system.io.stream.read%28v=vs.110%29.aspx) is called. The complete class reference is listed in [Class Reference.md](Class Reference.md).
