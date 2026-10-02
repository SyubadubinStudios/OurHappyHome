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
- [~] Suara ElevenLabs: 6 kalimat terekam; sisanya memakai babble sintetis (Rodin menolak permintaan berikutnya)
- [ ] Model tetangga, monyet, ular, petugas (sementara memakai mannequin prosedural)
- [ ] Rig & animasi hewan peliharaan (sementara memakai animasi prosedural)

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
- [ ] Pipeline CI rilis otomatis

## Catatan perbaikan selama pengembangan
- Titik pendekatan kursi makan semula berada di dalam meja, sehingga AI tidak pernah sampai. Sekarang titiknya dihitung di luar footprint, dan perjalanan punya batas waktu.
- Bahan makanan habis di tengah minggu. Sekarang orang tua berbelanja otomatis saat persediaan menipis.
- Barang yang rusak tanpa aktivitas "Perbaiki" tidak bisa diperbaiki. Sekarang semua barang rusak bisa diperbaiki.
- Makan bersama jarang terjadi. Sekarang ada panggilan "ayo makan bersama" saat masakan selesai dan pada pukul 07:00 & 18:45.
- Label nama kosong karena teks yang dipusatkan tergambar di luar tekstur. Sekarang teks digambar mulai dari padding.
