# CyberDot.Encoding.Base91

An implementation of Base91 (basE91) encoding in C#.

Base91 is a binary-to-text encoding that uses 91 of the 94 printable ASCII characters -
excluding space, apostrophe, hyphen and backslash. It packs input bits into a queue and
drains a pair of characters at a time, each pair holding 13 or 14 bits, so encoded output
is about 23% larger than the input, against 33% for Base64.

Unlike Base64/Base85-style encodings, Base91 has no fixed-width character group, so there
is no canonical padding to validate on decode: bits left over from a final, unpaired
character are simply dropped, exactly as the reference implementation does.

## Usage

```csharp
using CyberDot.Encoding.Base91;

var bytes = System.Text.Encoding.UTF8.GetBytes("hello world");
var encoded = Base91.Encode(bytes);

var decoded = Base91.Decode(encoded); // Output: hello world
```

### Streaming

`ToBase91Transform`/`FromBase91Transform` implement `ICryptoTransform`, so they plug
into `CryptoStream` for streaming encode/decode without buffering the whole payload in
memory. They default to UTF-8, or accept any `Encoding` via the constructor.

```csharp
using System.Security.Cryptography;

using var output = new MemoryStream();
using (var cryptoStream = new CryptoStream(output, new ToBase91Transform(), CryptoStreamMode.Write))
{
    sourceStream.CopyTo(cryptoStream);
}

using var decoded = new MemoryStream();
using (var cryptoStream = new CryptoStream(encodedStream, new FromBase91Transform(), CryptoStreamMode.Read))
{
    cryptoStream.CopyTo(decoded);
}
```

## License

The MIT License (MIT)
