# Dokumentasi Our Happy Home

**Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.**

| Halaman | Isi |
|---|---|
| [Panduan bermain](panduan-bermain.md) | Kontrol, kebutuhan, keluarga, memasak, sekolah, uang, membangun, kejadian darurat, mode dan bab |
| [Galeri screenshot](screenshots.md) | Semua tangkapan layar dari game yang berjalan |
| [Arsitektur](arsitektur.md) | Struktur solusi, simulasi, rendering ThreeNet, audio, UI dan penyimpanan |
| [Aset & pipeline](aset-dan-pipeline.md) | Concept art dan model 3D dari Rodin MCP, rigging dan animasi lewat Blender MCP, suara dan musik |
| [Build, run & test](build-dan-test.md) | Kebutuhan, perintah, mode screenshot dan pemecahan masalah |
| [Installer & paket rilis](installer.md) | Membangun dan memasang paket untuk Windows, Linux dan macOS |
| [Cakupan desain](cakupan-desain.md) | Setiap bagian dokumen desain dan cara game mewujudkannya, termasuk fitur tambahan |

![Key art](images/concept-key-art.jpg)

## Ringkasan singkat (English)

- **Play:** `dotnet run --project src/OurHappyHome`
- **Test:** `dotnet test`
- **Screenshots:** `dotnet run --project src/OurHappyHome -- --screenshots docs/images`
- Code layout: `src/OurHappyHome.Core` holds the simulation and has no UI dependencies,
  `src/OurHappyHome` holds the Avalonia app with ThreeNet rendering, audio and UI, and
  `tests/OurHappyHome.Tests` holds xUnit simulation tests.
