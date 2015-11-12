DieFledermaus format (.maus file)
=================================
Version 0.93
------------
The Die Fledermaus algorithm is simply the [DEFLATE algorithm](http://en.wikipedia.org/wiki/DEFLATE) with metadata and a magic number. The name exists solely to be a bilingual pun. Two versions are defined for use: 0.92 and 0.93. The format is specified in [Die Fledermaus Format.md](Die Fledermaus Format.md).

Class Library
=============
The library contains one public type, `DieFledermaus.DieFledermausStream`, which provides the same functionality as the [`System.IO.Compression.DeflateStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.deflatestream.aspx) or [`System.IO.Compression.GZipStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.gzipstream.aspx) classes. Unlike either of these classes, however, DieFledermausStream must read part of the underlying stream before [`Stream.Read()`](https://msdn.microsoft.com/en-us/library/system.io.stream.read%28v=vs.110%29.aspx) is called. The complete class reference is listed in [Class Reference.md](Class Reference.md).
