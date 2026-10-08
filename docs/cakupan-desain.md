# Cakupan dokumen desain

Pemetaan dari setiap bagian `Our_Happy_Home_Game_Design.md` ke implementasinya.

| § | Desain | Implementasi |
|---|---|---|
| 1 | 4 pilar: Bangun, Hidup, Jelajah, Lindungi | Mode bangun, simulasi harian, kota terbuka, skenario darurat dan penyelamatan |
| 2 | Keluarga & penampilan | Model Rodin sesuai pakaian (topi Ayah, jepit kerang Kakak, kaos "I ♥ Mom", baret merah Adik); keahlian & kepribadian di `FamilyMember.Create` |
| 3 | Loop inti | Bangun → rutinitas → sarapan bersama (07:00) → sekolah/kerja → uang & keahlian → belanja/jelajah → bangun rumah → aktivitas keluarga → kejadian → makan malam (18:45) → tidur |
| 4 | Keluarga yang hidup | Utility AI untuk semua anggota non-pemain setiap tick, termasuk jadwal, hobi, ketakutan dan kenangan (misalnya pancake bersama Ibu) |
| 5 | 8 kebutuhan | `Needs` (kesehatan, kenyang, energi, bahagia, kebersihan, tidur, senang-senang, keluarga); karakter yang lelah tertidur di sofa |
| 6 | Hubungan | Matriks berarah 5×5 dengan nilai awal sesuai contoh desain; bertengkar, minta maaf, berbaikan |
| 7 | Membangun rumah | 9 ruangan tambahan (sesuai daftar desain), perabot interaktif (blender membuat minuman, kompor, rak buku, TV) |
| 8 | Ekonomi | Gaji, usaha kue, jualan limun, gambar, kerajinan, tagihan, perabot, ruangan, hewan, rekreasi |
| 9 | Memasak | 20 resep dengan bahan, langkah, waktu, syarat keahlian, 5 tingkat kualitas, masak bersama dan kesalahan lucu |
| 10 | Sekolah | 6 mata pelajaran sebagai mini-game; IPA ditambah keahlian Memperbaiki membantu memperbaiki elektronik |
| 11 | Kalender | Tanggal Januari dari desain (3, 8, 12, 18, 24, 31) dan acara berulang |
| 12 | Cuaca | 7 jenis cuaca; hujan membuat orang berlari masuk; badai memicu listrik padam |
| 13 | Kejadian acak | Semua kejadian umum, jarang dan langka, plus **Badai Besar** multi-hari dengan semua tahapnya |
| 14 | Keamanan rumah | Kunci pintu, tutup jendela, lampu luar, alarm, kamera, ruang aman, menelepon bantuan, alat pemadam |
| 15 | Stamina | Ambang 100/60/20/0; aksi berat ditolak saat kelelahan; pemulihan baru mulai di atas 35% |
| 16 | Penyelamatan | Status Butuh Bantuan, Terjebak dan Lemas; hitung mundur "Aman dalam"; Ayah punya keahlian protection |
| 17 | Kegagalan | Layar "KELUARGA GAGAL — Tidak ada yang boleh tertinggal" dengan tombol "Ulangi Kejadian" (snapshot) |
| 18 | Kota | Peta sesuai diagram desain: gunung/kemah di utara, hutan di barat, sekolah di timur, pusat kota, klinik dan mal di selatan, pantai |
| 19 | Hewan peliharaan | Anjing, kucing, kelinci, hamster, ikan dengan lapar, bahagia, energi dan kepercayaan; anjing & kucing ter-rig (jalan, lari, duduk, tidur, menggonggong); anjing menggonggong jika ada bahaya |
| 20 | Acara keluarga & album | Ulang tahun, malam film, piknik, kemah, taman bermain, pesta kostum, foto otomatis di album dengan bingkai & stiker |
| 21 | Progres | 5 bab sesuai desain, lalu Kotak Pasir Keluarga |
| 22 | Cerita yang muncul sendiri | Contoh hujan → listrik padam → takut → senter → Ayah ke panel → cokelat hangat → berkumpul → kenangan terjadi lewat sistem yang saling terhubung |
| 23 | Kenangan | Peserta, tempat, jenis, perasaan, tanggal, foto, perubahan hubungan; muncul lagi dalam percakapan dan pilihan AI |
| 24 | Arah seni | 3D bergaya dan hangat; siang terang; malam dengan jendela hangat dan luar sejuk; hujan dengan partikel |
| 25 | Audio | Hujan, dapur, langkah, pintu, percakapan (babble atau rekaman), TV, burung, alat rumah tangga, hewan, cuaca; musik berganti mengikuti suasana |
| 26 | UI | Karakter, kebutuhan, stamina, mood, tujuan, jam & tanggal, cuaca, uang, panel keluarga (status & lokasi) |
| 27 | Aksesibilitas | Kesulitan, kontrol sederhana, bantuan membaca, subtitle, bantuan warna, intensitas darurat dikurangi, tutorial, tanpa luka grafis, jeda; Mode Santai dan Petualangan |
| 28–31 | Pembeda & visi | Rumah yang benar-benar ditinggali, lima karakter otonom, hubungan & kenangan, kerja sama keluarga sebagai inti |

## Fitur tambahan (di luar dokumen desain)

- Bisa **bermain sebagai anggota keluarga mana pun** (Tab), sementara anggota lain tetap otonom.
- **Tetangga & warga kota** dengan model ter-rig dan suara: Nenek Sari (kue), Pak Budi (pekerjaan kecil),
  Dimas (main bola), Bu Guru Rina (di ruang kelas), dr. Sinta (di klinik).
- **Gedung yang bisa dimasuki**: ruang kelas, supermarket dan klinik (v1.1).
- **Kostum & topi** dari lemari kostum (v1.1), pakaian otomatis menurut cuaca (v1.2).
- **Festival kota, hari libur nasional, musim, jadwal tetangga & persahabatan, mobil & angkot, liburan
  menginap** (v1.2).
- **Mini-game**: memasak (3 jenis tantangan), sekolah (6 jenis), kios limun, arkade tangkap bintang.
- **Usaha kue rumahan Ibu**, gambar Kak Nara yang dibeli tetangga, kerajinan, dan sakit flu dengan
  sup ayam atau klinik.
- **Wahana taman bermain**, istana pasir, api unggun, berenang di laut dan kolam rumah.
- **Cutaway dinding** gaya *doll house*, pintu yang membuka sendiri, kursor bangun dengan validasi.
- **11 pencapaian**, rapor sekolah, buku kas keluarga, dan ramalan cuaca besok.
- **Dua bahasa** (ID/EN), autosave, dan mode screenshot otomatis.
- **Langit, pantai dan perkemahan yang hidup** dengan pengunjung, burung, kupu-kupu dan kunang-kunang (v1.3).
- **Rumah dua lantai** dengan tangga, balkon, loteng dan studio; sepeda, halte dan angkot (v1.4).
- **55 test otomatis** untuk simulasi.
