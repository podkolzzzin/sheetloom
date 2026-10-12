# Bundled dependency licenses

Upstream texts for the runtime NuGet packages listed in [the inventory](../THIRD_PARTY_NOTICES.md). OpenXML covers both DocumentFormat.OpenXml packages. Keep these files when redistributing the renderer.

- `ClosedXML.LICENSE`: [upstream](https://raw.githubusercontent.com/ClosedXML/ClosedXML/b4ebe47bd3ecebf8480dea7422188d16b35b9d9a/LICENSE)
- `ClosedXML.Parser.LICENSE`: [upstream](https://raw.githubusercontent.com/ClosedXML/ClosedXML.Parser/658973aeedc2fe289e0ccba2bb9959f67692ded5/LICENSE.txt)
- `OpenXML.LICENSE`: [upstream](https://raw.githubusercontent.com/dotnet/Open-XML-SDK/f9e6ad7524e22b6b1f932deabc9055201b7870c0/LICENSE)
- `OpenXML.NOTICE`: [upstream](https://raw.githubusercontent.com/dotnet/Open-XML-SDK/f9e6ad7524e22b6b1f932deabc9055201b7870c0/NOTICE)
- `RBush.LICENSE`: [upstream](https://raw.githubusercontent.com/viceroypenguin/RBush/b8690322abac35d835baa2fdca4a3918ed77a910/LICENSE)
- `SixLabors.Fonts.LICENSE`: [upstream](https://raw.githubusercontent.com/SixLabors/Fonts/32bef42997adb10268369ca149777f00e4241ce9/LICENSE)
- `ExcelNumberFormat.LICENSE`: [upstream](https://raw.githubusercontent.com/andersnm/ExcelNumberFormat/v1.1.0/LICENSE)
- `System.IO.Packaging.*`: copied from the System.IO.Packaging 8.0.1 NuGet package.

The container also retains Node notices at `/app/licenses/Node.LICENSE`, .NET notices under `/usr/share/dotnet`, npm package licenses in `node_modules`, and system-package notices under `/usr/share/doc`. Node notices are copied from the same image stage as the Node binary.
