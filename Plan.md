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

## Fase 3 — Dunia yang lebih hidup ✅ (v1.2)

- Wajah & bahasa tubuh: tulang **Jaw** di semua rig (ThreeNet 0.7 belum punya morph target), mulut
  bergerak saat berbicara; pose **Happy** dan **Sad** mengikuti suasana hati; pose lama (membaca,
  memasak, berlari) diperbaiki agar kepala menunduk dan badan condong dengan benar
- Musim hujan & kemarau, hari libur nasional (Tahun Baru, Hari Buruh, Pancasila, 17 Agustus, Natal),
  libur semester, Hari Kartini, Hari Anak, Sumpah Pemuda, Hari Guru, Hari Ibu
- Pakaian otomatis menurut cuaca: topi hujan saat hujan, topi jerami saat terik di musim kemarau
- Tetangga dengan jadwal sendiri (sekolah, belanja, taman, klinik) dan persahabatan; Dimas datang
  ke rumah saat sudah berteman baik
- Festival kota bulanan & 17 Agustus di taman: lomba makan kerupuk, tarik tambang, jajanan, lempar
  gelang, dan kembang api pukul 20:00
- Transportasi: mobil bersama Ayah (paling cepat) dan angkot, selain jalan kaki/sepeda
- Liburan menginap: tidur di tenda di bumi perkemahan atau di penginapan pantai
- 3 pencapaian baru: Bintang Festival, Liburan Menginap, Tetangga Baik

Dipindah ke v1.3: rumah dua lantai (tangga, loteng, balkon), sinkronisasi bibir dari suara
sungguhan, ekspresi wajah dengan morph target saat ThreeNet mendukungnya.

## Fase 3b — Lingkungan yang hidup & langit ✅ (v1.3)

- Langit: kubah langit dengan gradasi sesuai jam, awan yang bergerak dan berubah warna (senja, malam,
  mendung), bintang di malam hari; kabut lebih tipis dan kamera lebih rendah saat bepergian
- Pantai: laut biru dengan riak, pasir basah, air dangkal untuk berjalan, buih ombak yang bergerak,
  pohon kelapa (Rodin), warung bambu (Rodin), menara penjaga pantai, kursi & payung, perahu jukung,
  handuk, istana pasir, bola pantai, papan selancar, net voli, burung camar terbang dan bersuara
- Bumi perkemahan: hutan pinus lebat (Rodin), danau, jalan setapak, tenda kubah, bangku kayu di sekitar
  api unggun, perkemahan Pramuka dengan api unggunnya sendiri, tiang bendera, batu dan bunga, kupu-kupu
  di siang hari dan kunang-kunang di malam hari
- Pengunjung: turis di pantai (termasuk yang berenang), anggota Pramuka dan pendaki di perkemahan
- Model tambahan dibuat langsung di Blender lewat Blender MCP saat kredit Rodin habis
  (`tools/blender/model_props.py`)

## Fase 3c — Rumah bertingkat (v1.4)

- Rumah dua lantai dengan tangga, loteng dan balkon
- Ekspresi wajah dengan morph target dan sinkronisasi bibir dari amplitudo suara
- Sepeda yang terlihat saat dikendarai, halte angkot di kota

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
