DieFledermaus format (.maus file)
=================================
* Version 0.94

The DieFledermaus file format is simply a [DEFLATE](http://en.wikipedia.org/wiki/DEFLATE)-compressed file, with metadata and a magic number. The name exists solely to be a bilingual pun. Three versions are defined for use: 0.92 (depreciated), 0.93 (depreciated), and 0.94. The file format is specified in [DieFledermaus Format.md](DieFledermaus Format.md).

Features
--------
* DEFLATE compression. You can also specify that it's not compressed at all.
* Storing the filename of the original file.
* AES encryption, using either a binary key or a password. This includes encrypting the filename.

DieFlede
========
* Version 0.0.2.0

A command-line utility for creating DieFledermaus files. The name is an even worse pun.

Usage:
------
Compress a file:
```
DieFled.exe -cf [archive.maus] [input filename]
```

Decompress a file:
```
DieFled.exe -xf [archive.maus] [output filename]
```

Show extended help:
```
DieFled.exe --help
```

On Unix and OSX systems, use `mono DieFlede.exe`. Make sure you have [Mono](http://www.mono-project.com/) installed.

Bat icon adapted from an icon created by [Marianna Nardella](https://thenounproject.com/term/bat/216021/), and released under the [Creative Commons 3.0 Attribution](http://creativecommons.org/licenses/by/3.0/us/) license.	

Class Library
=============
* Version 0.1.0.0

The library contains one public type, `DieFledermaus.DieFledermausStream`, which provides much the same functionality as the [`System.IO.Compression.DeflateStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.deflatestream.aspx) or [`System.IO.Compression.GZipStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.gzipstream.aspx) classes. Unlike either of these classes, however, DieFledermausStream must read part of the underlying stream before [`Stream.Read()`](https://msdn.microsoft.com/en-us/library/system.io.stream.read%28v=vs.110%29.aspx) is called. The complete class reference is listed in [Class Reference.md](Class Reference.md).
Disclaimer
==========
**This project is currently in-development. Features and functionality may be changed or removed without notice.**
