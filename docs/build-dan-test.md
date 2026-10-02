# Build, run & test

## Kebutuhan

- .NET SDK 10
- Windows 10/11 (DirectX 12), Linux (Vulkan) atau macOS (Metal). Game sudah diuji di Intel UHD 620.
- ThreeNet diambil dari NuGet: `ThreeNet` dan `ThreeNet.Avalonia` 0.7.0, sudah termasuk inti native Rust/wgpu.
  Tidak perlu Rust untuk membangun game.

## Perintah

```bash
dotnet build OurHappyHome.slnx                       # build semua
dotnet run --project src/OurHappyHome                # main
dotnet test                                          # 37 test simulasi (±6 detik)
dotnet run --project src/OurHappyHome -- --screenshots docs/images   # ambil ulang semua screenshot
dotnet publish src/OurHappyHome -c Release -r win-x64 --self-contained   # paket rilis
```

## Test

`tests/OurHappyHome.Tests/SimulationTests.cs` mencakup:

- lima anggota keluarga, posisi awal tidak menabrak
- keluarga hidup sendiri selama 2 hari dan 7 hari (3 seed, 3 mode) tanpa kelaparan, dengan makan
  bersama dan kenangan
- anggota yang dikendalikan tidak dijalankan oleh AI, dan collision dinding bekerja
- pintu antar-ruangan dan navigasi A* dari kamar ke dapur
- kualitas masakan mengikuti keahlian, dengan rentang Gagal sampai Sempurna
- ambang stamina 100/60/20/0 dan aksi berat ditolak saat kelelahan
- simpan dan muat tanpa ada data yang hilang
- semua 12 skenario dapat dimulai dan dijalankan
- penyelamatan yang gagal dapat diulang, dan anggota yang diselamatkan menjadi aman
- mode Santai jauh lebih jarang memicu bahaya dibanding mode Petualangan
- membangun ruangan memakan biaya dan menambah dinding, dan perabot tidak bisa menumpuk
- tanggal-tanggal kalender dari dokumen desain, hadiah yang sesuai kesukaan, soal sekolah valid,
  dan semua pintu masuk tempat di kota bisa dicapai

## Mode screenshot

`--screenshots <folder>` menjalankan `Views/ScreenshotDirector.cs`. Mode ini membuka menu dan halaman
Tentang, memulai permainan baru (seed tetap), lalu melompat ke beberapa waktu: sarapan, mini-game,
mode bangun, badai, listrik padam, kebakaran, perjalanan ke taman, pantai, kemah dan sekolah, serta
peta, album dan menu jeda. Setiap momen disimpan sebagai PNG dari jendela asli, lalu aplikasi keluar.

## Pemecahan masalah

| Gejala | Solusi |
|---|---|
| Jendela 3D hitam atau tertulis "Three.Net: …" | Perbarui driver GPU (DX12/Vulkan/Metal). Pilih **Grafis → Cepat** di Pengaturan. |
| FPS rendah | Pilih kualitas **Cepat** atau **Sedang**: render scale, bayangan, SSAO dan MSAA akan dikurangi. |
| Tidak ada suara | Pastikan ada perangkat audio output. Tanpa perangkat, game tetap berjalan tanpa suara. |
| Simpanan lama tidak bisa dimuat | Hapus file di `%AppData%/OurHappyHome`. |
