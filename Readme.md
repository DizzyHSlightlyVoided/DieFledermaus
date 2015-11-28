DieFledermaus format (.maus file)
=================================
* Version 0.96

The DieFledermaus file format is simply a [DEFLATE](http://en.wikipedia.org/wiki/DEFLATE)- or [Lempel-Ziv-Markov chain](https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Markov_chain_algorithm)-compressed file, with metadata and a magic number. The name exists solely to be a bilingual pun. The file format is specified in [DieFledermaus Format.md](DieFledermaus Format.md).

It has been extended with the DieFledermauZ [archive file](https://en.wikipedia.org/wiki/Archive_file) format, which can contain multiple files. The DieFledermauZ format is specified in [DieFledermauZ Format.md](DieFledermauZ Format.md).

The project has two components: the DieFledermaus class library, which is intended for programmers, and which allows the full range of capabilities when creating and consuming DieFledermaus and DieFledermauZ files; and the DieFlede command-line utility, which is intended for end users, and which is meant to more accurately reflect the "best practices" when encoding DieFledermaus files.

Features
--------
* DEFLATE compression, LZMA compression, or no compressoin at all.
* Storing the filename of the original file.
* AES encryption, using either a binary key or a password. This includes encrypting the filename.

DieFlede
========
* Version 0.0.4.0

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

Class Library
=============
* Version 0.1.3.0

The library contains one public type, `DieFledermaus.DieFledermausStream`, which provides much the same functionality as the [`System.IO.Compression.DeflateStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.deflatestream.aspx) or [`System.IO.Compression.GZipStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.gzipstream.aspx) classes. Unlike either of these classes, however, DieFledermausStream must read part of the underlying stream before [`Stream.Read()`](https://msdn.microsoft.com/en-us/library/system.io.stream.read%28v=vs.110%29.aspx) is called. The complete class reference is listed in [Class Reference.md](Class Reference.md).
Disclaimer
==========
**This project is currently in-development. Features and functionality may be changed or removed without notice.**
