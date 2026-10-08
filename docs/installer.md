# Installer & paket rilis

Skrip di folder `installer/` membuat paket untuk setiap platform. Hasilnya ditulis ke `artifacts/`.
Semua paket bersifat **self-contained**: .NET 10, Avalonia, dan inti native ThreeNet (wgpu) sudah ikut
dibundel, jadi pengguna tidak perlu memasang apa pun.

```
installer/
├─ build-all.ps1              build semua platform sekaligus
├─ windows/
│  ├─ build-windows.ps1       zip portabel (+ setup.exe jika Inno Setup 6 terpasang)
│  ├─ install.cmd / install.ps1 / uninstall.ps1   installer per-pengguna (tanpa admin)
│  └─ OurHappyHome.iss        skrip Inno Setup (setup.exe klasik, Bahasa Indonesia & Inggris)
├─ linux/
│  ├─ build-linux.sh          tar.gz (+ AppImage jika appimagetool tersedia)
│  ├─ install.sh / uninstall.sh
│  └─ ourhappyhome.desktop    entri menu aplikasi
└─ macos/
   ├─ build-macos.sh          "Our Happy Home.app" untuk Apple Silicon & Intel (+ .zip/.dmg di macOS)
   └─ Info.plist
```

## Membangun paket

```powershell
# Windows (PowerShell). Linux & macOS ikut dibangun bila Git Bash / bash tersedia.
powershell -ExecutionPolicy Bypass -File installer/build-all.ps1 -Version 1.4.0
powershell -ExecutionPolicy Bypass -File installer/build-all.ps1 -Platforms win
```

```bash
# Linux / macOS / Git Bash
bash installer/linux/build-linux.sh 1.4.0
bash installer/macos/build-macos.sh 1.4.0
```

| Paket | Isi | Ukuran ±|
|---|---|---|
| `OurHappyHome-<v>-win-x64-portable.zip` | Game + `install.cmd` | 62 MB |
| `OurHappyHome-<v>-win-x64-setup.exe` | Wizard Inno Setup (jika ISCC ada) | — |
| `OurHappyHome-<v>-linux-x64.tar.gz` | Game + `install.sh` + `.desktop` + ikon | 60 MB |
| `OurHappyHome-<v>-x86_64.AppImage` | Satu file, langsung jalan (jika appimagetool ada) | — |
| `OurHappyHome-<v>-macos-arm64 / x64` (`.zip` + `.dmg` di macOS, `.tar.gz` di luar macOS) | `Our Happy Home.app` | 60 MB |

## Memasang

### Windows

- **Portabel:** ekstrak zip, lalu jalankan `OurHappyHome.exe`. Selesai.
- **Pasang:** klik dua kali `install.cmd`. Game disalin ke `%LOCALAPPDATA%\Programs\OurHappyHome`,
  shortcut dibuat di Start Menu dan desktop, dan game muncul di *Apps & features*. Tidak perlu hak admin.
- **Copot:** lewat *Apps & features*, atau jalankan
  `powershell -File "%LOCALAPPDATA%\Programs\OurHappyHome\uninstall.ps1"`. Tambahkan `-RemoveSaves`
  untuk ikut menghapus simpanan.
- **setup.exe:** wizard klasik dengan pilihan folder dan ikon desktop.

Kebutuhan: Windows 10/11 64-bit dengan GPU DirectX 12.

### Linux

```bash
tar -xzf OurHappyHome-1.4.0-linux-x64.tar.gz
cd OurHappyHome && ./install.sh          # ke ~/.local/share/OurHappyHome, perintah: ourhappyhome
~/.local/share/OurHappyHome/uninstall.sh  # copot (tambah --remove-saves untuk hapus simpanan)
```

Atau jalankan langsung dengan `./OurHappyHome` tanpa memasang. Kebutuhan: x86-64 dengan driver
Vulkan, X11 atau Wayland (XWayland).

### macOS

Seret `Our Happy Home.app` ke folder *Applications*. Saat pertama kali dibuka, klik kanan lalu pilih
**Open**, karena aplikasi hanya ditandatangani ad-hoc. Untuk distribusi publik, tanda tangani dengan
Developer ID dan lakukan notarisasi:

```bash
codesign --deep --force --options runtime --sign "Developer ID Application: ..." "Our Happy Home.app"
xcrun notarytool submit OurHappyHome-1.4.0-macos-arm64.zip --apple-id ... --wait
```

Kebutuhan: macOS 12 atau lebih baru (Metal). Ada paket terpisah untuk Apple Silicon (`arm64`) dan
Intel (`x64`).

## Lokasi data pengguna

| OS | Simpanan, foto album, pengaturan |
|---|---|
| Windows | `%APPDATA%\OurHappyHome` |
| Linux | `~/.config/OurHappyHome` |
| macOS | `~/Library/Application Support/OurHappyHome` (atau `~/.config/OurHappyHome`) |

## CI & rilis otomatis (GitHub Actions)

| Workflow | Pemicu | Isi |
|---|---|---|
| `.github/workflows/ci.yml` | push ke `main`, pull request | build + 55 test di Windows, Ubuntu, macOS |
| `.github/workflows/release.yml` | push tag `v*` atau manual (*Run workflow* + versi) | test, lalu build installer di 3 OS → GitHub Release |

Merilis versi baru:

```bash
git tag v1.0.1
git push origin v1.0.1
```

Job `windows` memasang Inno Setup (setup.exe + zip portabel), `linux` memasang appimagetool
(AppImage + tar.gz), `macos` membuat `.zip` + `.dmg` untuk arm64 dan x64. Job `publish`
mengumpulkan semuanya, membuat `SHA256SUMS.txt`, lalu membuat rilis dengan catatan otomatis.
Checkout memakai Git LFS agar model 3D ikut terbungkus.
