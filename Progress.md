# Our Happy Home — Progress

Checklist pengembangan. Roadmap ada di [Plan.md](Plan.md).

## 2026-10-02 · v1.0

### Aset (Rodin MCP & Blender MCP)
- [x] Concept art: key art, lembar karakter keluarga, interior rumah, eksterior, peta kota
- [x] Model 3D karakter T-pose: Ayah, Ibu, Kak Nara, Raka, Dinda
- [x] Model 3D prop: sofa, ranjang loteng, ranjang ganda, dapur, kulkas, meja makan, meja belajar, mobil,
      ayunan, lemari TV, pohon bunga, rak buku, anjing, kucing
- [x] Optimasi prop (decimate ±6k face, tekstur 1024 JPEG): ±9 MB → ±0,4 MB per model
- [x] Skrip auto-rig: deteksi sendi dari mesh, 19 tulang, bone-heat + bobot jarak
- [x] 12 animasi (Idle, Walk, Run, Wave, Sit, Sleep, Cook, Cheer, Scared, Talk, Work, Read), dirender lewat Blender MCP
- [x] Uji skinning & klip animasi di ThreeNet
- [x] Potret HUD & ikon aplikasi dari concept art
- [x] Suara ElevenLabs: 6 kalimat terekam (sisanya selesai di v1.1)
- [x] Model tetangga, monyet, ular, petugas (selesai di v1.1)
- [x] Rig & animasi hewan peliharaan (selesai di v1.1)

### Simulasi (`OurHappyHome.Core`)
- [x] Waktu, kalender (tanggal dari desain, ulang tahun, acara berulang), kecepatan 1/2/4×, malam dipercepat
- [x] 8 kebutuhan, stamina 100/60/20/0, mood dengan moodlet, 26 keahlian, kepribadian
- [x] Hubungan berarah 5×5, bertengkar & berbaikan
- [x] Utility AI untuk semua anggota otonom, termasuk jadwal sekolah/kerja/belanja dan makan bersama
- [x] Rumah dengan slot ruangan, dinding & pintu otomatis, 40+ perabot, collision, A*
- [x] Peta kota: 13 tempat, tetangga & NPC
- [x] Ekonomi: gaji, tagihan, usaha kue, limun, gambar, kerajinan, toko
- [x] Memasak: 14 resep, 5 kualitas, masak bersama, kesalahan lucu
- [x] Sekolah: 6 mata pelajaran dengan bank soal
- [x] Cuaca 7 jenis dengan ramalan, petir, listrik padam
- [x] 12 skenario + EventDirector (Santai/Normal/Petualangan, intensitas dikurangi)
- [x] Penyelamatan (Butuh Bantuan/Terjebak/Lemas), hitung mundur, Ulangi Kejadian (snapshot)
- [x] Kenangan dengan foto, 5 bab + sandbox, 11 pencapaian, hewan peliharaan
- [x] Simpan/muat JSON + autosave pagi

### Presentasi (`OurHappyHome`)
- [x] Renderer ThreeNet: rumah (cutaway, jendela menyala, pintu otomatis, atap), kota, karakter, hewan, aktor
- [x] Siang/malam, cuaca (kabut, hujan, angin, kilat), bayangan, bloom, SSAO, kualitas grafis
- [x] Partikel: hati, bintang, konfeti, kembang api, asap, api, uap, percikan, tepung, daun, zzz, not, koin, hujan
- [x] Audio: 6 lagu prosedural adaptif, ±50 efek, 7 ambience, suara karakter + babble
- [x] Menu utama (key art), permainan baru (pilih mode & nama), muat, pengaturan, tentang + kredit bergulir
- [x] HUD: karakter, kebutuhan, stamina, mood, tujuan, jam/tanggal, cuaca, uang, panel keluarga, banner darurat
- [x] Panel: jeda, jurnal (bab/kalender/pencapaian/keuangan/rapor), keluarga, album, peta, toko, resep,
      hadiah, adopsi, barang, bantuan, gagal, kartu bab
- [x] Mini-game: memasak, sekolah, limun, arkade
- [x] Mode bangun: ruangan, perabot (ghost + validasi), pindah/jual, cat, lantai
- [x] Aksesibilitas: teks, subtitle, bantuan membaca, bantuan warna, intensitas, kontrol sederhana, tutorial, kesulitan, jeda
- [x] Dua bahasa (ID/EN)

### Kualitas & rilis
- [x] 37 test xUnit (multi-hari, 3 mode, skenario, simpan/muat, navigasi, masakan, stamina)
- [x] Mode screenshot otomatis (`--screenshots`), dengan 20 screenshot di `docs/images`
- [x] README dua bahasa + dokumentasi `docs/`
- [x] Installer: Windows (zip portabel + install.ps1, Inno Setup .iss), Linux (tar.gz + install.sh, AppImage opsional),
      macOS (.app arm64/x64, .zip/.dmg di macOS)
- [x] Uji pasang, jalankan dan copot di Windows
- [ ] Uji paket Linux & macOS di perangkat asli
- [ ] Tanda tangan kode (Authenticode / Developer ID) dan notarisasi
- [x] CI (build + test 3 OS) dan rilis otomatis installer ke GitHub Releases

## 2026-10-08 · v1.1

### Aset
- [x] 39 kalimat suara baru: keluarga, Nenek Sari, Pak Budi, Dimas, Bu Guru Rina, dr. Sinta (total 45)
- [x] Model Rodin + rig Blender MCP (12 animasi): Nenek Sari, Pak Budi, Dimas, Bu Guru Rina, dr. Sinta,
      polisi, pemadam kebakaran, tim SAR, orang asing
- [x] Anjing & kucing berdiri dari Rodin, rig berkaki empat baru (`rig_animal.py`): Idle, Walk, Run, Sit, Sleep, Bark
- [x] Model monyet, ular dan pohon dari Rodin (dioptimasi sebagai prop)
- [x] `preview_poses.py` untuk merender beberapa pose sekaligus; kamera `preview.py` diperbaiki

### Game
- [x] `AnimatedFigure`: model ber-rig per instance untuk tetangga, pengunjung skenario dan hewan, dengan cross-fade
- [x] Animasi Talk saat berbicara; tetangga berhenti dan menghadap pemain saat diajak ngobrol (dengan suara)
- [x] Interior sekolah, supermarket dan klinik: masuk/keluar bersama keluarga, meja layanan, Bu Guru Rina di kelas,
      dr. Sinta di klinik, kamera langsung pindah
- [x] 6 resep, 4 bahan, 5 perabot, 12 warna cat baru
- [x] Kostum & topi (5 item, lemari kostum dari lemari baju atau panel barang), bonus saat Pesta Kostum
- [x] Album dengan 5 bingkai dan stiker, tersimpan di save
- [x] Kalimat dan kunci suara diselaraskan (mis. "Aku takut... gelap sekali.", "Terima kasih, Kakak!")
- [ ] Ekspresi wajah dengan shape key & sinkronisasi bibir sungguhan (dipindah ke v1.2)

### Kualitas
- [x] 41 test xUnit (baru: interior, belanja di dalam supermarket, resep, kostum & dekorasi album tersimpan)
- [x] 28 screenshot otomatis (8 baru) dan gambar rig tetangga & hewan

## 2026-10-08 · v1.2

### Aset (Blender MCP)
- [x] Tulang Jaw di 14 rig manusia (bobot wajah bagian bawah dari posisi hidung), tidak dikunci di klip
      (`strip_channels`) sehingga game bisa menggerakkannya
- [x] Aksi baru Happy & Sad; perbaikan arah sumbu X tulang Spine/Chest/Head di semua pose lama
- [x] Semua rig keluarga & tetangga diekspor ulang lewat Blender MCP

### Game
- [x] Bahasa tubuh menurut suasana hati (Happy/Sad), mulut bergerak saat bicara (keluarga & tetangga)
- [x] Musim hujan/kemarau di HUD, hari libur nasional & libur semester (`IsWorkDay`, `IsSchoolDay`)
- [x] Hari Anak (kado), Hari Ibu (hadiah bernilai ganda + kenangan), Hari Guru (bunga untuk Bu Guru)
- [x] Topi hujan otomatis & topi jerami di musim kemarau (`AccessoryAuto`)
- [x] Jadwal tetangga (`NpcStop`), persahabatan (Kenalan → Teman → Teman baik → Sahabat), kunjungan Dimas
- [x] Festival kota: stan, panggung, bendera merah putih, umbul-umbul, lampion, kembang api, suara kembang api
- [x] Mini-game Lomba Makan Kerupuk & Tarik Tambang (kekuatan tim = jumlah anggota keluarga)
- [x] Mode perjalanan: jalan kaki, mobil bersama Ayah, angkot
- [x] Menginap di tenda (bumi perkemahan) atau Penginapan Pantai
- [x] 3 pencapaian baru
- [ ] Rumah dua lantai (dipindah ke v1.3)

### Kualitas
- [x] 50 test xUnit (baru: kalender & musim, jadwal tetangga, festival, mode perjalanan, menginap, pakaian
      otomatis; uji seminggu kini 6 seed)
- [x] 35 screenshot otomatis (7 baru)

## Catatan perbaikan selama pengembangan
- Titik pendekatan kursi makan semula berada di dalam meja, sehingga AI tidak pernah sampai. Sekarang titiknya dihitung di luar footprint, dan perjalanan punya batas waktu.
- Bahan makanan habis di tengah minggu. Sekarang orang tua berbelanja otomatis saat persediaan menipis.
- Barang yang rusak tanpa aktivitas "Perbaiki" tidak bisa diperbaiki. Sekarang semua barang rusak bisa diperbaiki.
- Makan bersama jarang terjadi. Sekarang ada panggilan "ayo makan bersama" saat masakan selesai dan pada pukul 07:00 & 18:45.
- Label nama kosong karena teks yang dipusatkan tergambar di luar tekstur. Sekarang teks digambar mulai dari padding.
- (v1.1) AI mengepel di dapur padahal genangan ada di kamar mandi, sehingga skenario keran bocor tidak pernah selesai dan tidak ada yang memasak. Sekarang tugas Bersih-bersih menuju genangan terdekat.
- (v1.1) Topi kostum muncul raksasa di pinggang. Penyebabnya: pemutar animasi milik pengunjung yang sudah dihapus masih berjalan, dan ThreeNet memakai ulang id node-nya untuk node baru. Sekarang animasi dihentikan sebelum node dihapus.
- (v1.1) Pratinjau "side" di Blender memutar kamera 90°, sehingga anjing & kucing tampak berbaring. Kamera pratinjau sekarang memakai sumbu atas yang benar.
- (v1.2) Ibu terus mencoba memasak resep yang bahannya habis (pemilih resep belum mengenal resep v1.1 dan jatuh ke "telur" yang juga gagal), sehingga tidak ada yang belanja dan keluarga kelaparan. Pemilih resep kini mengenal resep baru dan hanya memilih masakan yang bisa dibuat.
- (v1.2) Pose Read/Cook/Work/Run membuat kepala mendongak dan badan condong ke belakang karena tanda sumbu X terbalik untuk tulang punggung dan kepala. Sudah diperbaiki di skrip rig.
