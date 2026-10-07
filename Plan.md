# Our Happy Home — Roadmap

**Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.**

Roadmap pengembangan. Status tiap tugas dicatat di [Progress.md](Progress.md).

## Visi

Game simulasi keluarga 3D yang hangat: membangun rumah, menjalani keseharian bersama lima anggota
keluarga yang hidup sendiri, menjelajah kota, dan saling melindungi saat keadaan darurat.
*Tidak ada yang boleh tertinggal.*

## Fase 1 — Fondasi ✅ (v1.0)

- Aset: concept art, model 3D (Rodin MCP), rigging & 12 animasi (Blender MCP), suara ElevenLabs
- Solusi .NET 10: `OurHappyHome.Core` (simulasi), `OurHappyHome` (Avalonia + ThreeNet), test xUnit
- Simulasi lengkap: 5 anggota otonom, kebutuhan, stamina, mood, keahlian, hubungan, kenangan
- Rumah yang bisa dibangun, kota yang bisa dijelajahi, cuaca, kalender, ekonomi, memasak, sekolah
- 12 kejadian termasuk Badai Besar, sistem penyelamatan, "Tidak ada yang boleh tertinggal"
- Mode Santai/Normal/Petualangan, 5 bab, 11 pencapaian, album kenangan
- Efek visual, musik & efek suara prosedural, UI ramah anak, aksesibilitas, dua bahasa
- Menu Tentang dengan kredit bergulir, dokumentasi & screenshot, installer per platform

## Fase 2 — Polesan & konten ✅ (v1.1)

- Semua kalimat suara direkam lewat ElevenLabs (Ayah, Ibu, Kak Nara, Raka, Dinda, tetangga, dr. Sinta)
- Model Rodin + rig Blender MCP untuk Nenek Sari, Pak Budi, Dimas, Bu Guru Rina, dr. Sinta, polisi,
  pemadam kebakaran, tim SAR dan orang asing; model Rodin untuk monyet, ular dan pohon
- Rig hewan berkaki empat (`tools/blender/rig_animal.py`): anjing & kucing dengan Idle, Walk, Run, Sit,
  Sleep, Bark
- Animasi Talk saat berbicara (keluarga & tetangga) sebagai pengganti sederhana sinkronisasi bibir
- Interior sekolah, supermarket dan klinik yang bisa dimasuki bersama keluarga
- 6 resep baru (Mi Goreng, Gado-Gado, Bubur Ayam, Pisang Goreng, Martabak Manis, Es Teh Manis),
  5 perabot baru (akuarium, bean bag, lukisan, mesin arkade, ayunan jaring), 12 warna cat baru
- Kostum & topi dari lemari kostum (topi pesta, topi jerami, kupluk, mahkota, bando telinga kucing)
- Album dengan bingkai dan stiker

Dipindah ke v1.2: ekspresi wajah dengan shape key dan sinkronisasi bibir sungguhan, pakaian yang
berganti otomatis per musim.

## Fase 3 — Dunia yang lebih hidup (v1.2)

- Ekspresi wajah (shape key: senyum, sedih, takut) dan sinkronisasi bibir untuk suara
- Pakaian yang berganti otomatis per musim dan cuaca

- Tetangga dengan jadwal sendiri, persahabatan anak-anak dengan teman sekolah
- Rumah dua lantai dengan tangga, loteng, balkon
- Kendaraan: naik mobil bersama Ayah, sepeda yang bisa dikendarai, transportasi umum
- Festival kota dengan mini-game (lomba makan kerupuk, tarik tambang, kembang api)
- Liburan menginap beberapa hari (pantai, gunung) dengan kenangan khusus
- Musim hujan & kemarau, hari libur nasional, kalender yang lebih kaya

## Fase 4 — Platform & komunitas (v2.0)

- Dukungan gamepad penuh (ThreeNet `Gamepads`) dan antarmuka untuk TV/handheld
- Ko-op lokal/LAN, misalnya dua anak bermain bersama (ThreeNet `NetworkSession`)
- Berbagi desain rumah dan album keluarga (ekspor/impor)
- Build Android dan iOS (ThreeNet mendukung keduanya) serta versi web (WASM)
- Lokalisasi tambahan (Jawa, Sunda, Melayu)
- Distribusi: Steam / itch.io / Microsoft Store, rilis otomatis lewat CI

## Prinsip

1. Ramah keluarga: tanpa kekerasan dan tanpa adegan menakutkan; bahaya diselesaikan dengan perlindungan dan penyelamatan.
2. Sistem saling terhubung sehingga cerita muncul sendiri, bukan misi yang ditulis kaku.
3. Aksesibel untuk anak-anak: ikon di mana-mana, teks besar, kontrol sederhana, bisa dijeda.
4. Setiap fitur baru diuji di `OurHappyHome.Tests` dan didokumentasikan di `docs/`.
