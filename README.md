# Suarakata — Speech to Text (Whisper)

**Suarakata** adalah aplikasi desktop **Windows (C# / .NET Framework 4.8, WinForms)**
untuk mengubah rekaman suara (voice note WhatsApp, mp3, dll.) menjadi teks secara
**offline** menggunakan [Whisper.net](https://github.com/sandrohanea/whisper.net)
(binding whisper.cpp dari OpenAI Whisper).

## Fitur

- Transkripsi offline, akurasi tinggi (mendukung Bahasa Indonesia & banyak bahasa lain).
- **Antarmuka modern** dengan **mode terang & gelap** (tombol bulan/matahari di header),
  kartu membulat, area seret-dan-lepas, dan tombol datar bergaya Windows 11.
- Seluruh kontrol digambar sendiri agar seragam: **dropdown**, **menu popup**, **kotak dialog**,
  **kotak centang**, dan **scrollbar tipis** pada kotak hasil, tanpa kontrol Windows lawas.
- **Pengelola Model** khusus: unduh, **jeda**, **lanjutkan**, batalkan, impor, dan hapus model.
- Unduhan model bisa **dijeda lalu dilanjutkan** kapan saja, termasuk setelah aplikasi ditutup
  (kemajuan tersimpan di berkas `.part` memakai HTTP Range).
- Alternatif **unduh lewat browser**: buka tautan model di peramban, unduh di sana
  (bisa dijeda/dilanjutkan peramban), lalu **impor** berkasnya ke aplikasi.
- Pilih model: **Tiny / Base / Small / Medium / Large-v3**.
- Pilih bahasa atau **auto-deteksi**.
- Opsi **sertakan waktu** (timestamp per segmen).
- **Progres nyata** saat transkripsi (berdasarkan durasi audio) dan tombol **Batalkan**.
- Penghitung kata/karakter, tombol **Simpan .txt** dan **Salin**.
- Pilihan model, bahasa, timestamp, dan tema tersimpan otomatis di
  `%AppData%\Suarakata\settings.cfg`.
- **Mode CLI** untuk otomatisasi / batch, termasuk mengunduh model.

## Persyaratan

- Windows 10/11 (native runtime Whisper.net direkomendasikan Windows 11+).
- CPU x64 dengan dukungan **AVX, AVX2, FMA, F16C** (CPU modern umumnya mendukung).
- [Microsoft Visual C++ Redistributable (x64)](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist) versi VS 2022+.
- **ffmpeg** — sudah dibundel di folder aplikasi (`ffmpeg.exe`). Bisa juga dari PATH.
- Untuk build: **.NET SDK 8+** (`dotnet`). Reference assembly net48 disediakan lewat paket NuGet.

## Build

```powershell
dotnet build .\Suarakata.sln -c Release
```

Output di `bin\Release\Suarakata.exe`. Native `whisper.dll` (win-x64) otomatis
tersalin ke `bin\Release\runtimes\`.

> Catatan: letakkan `ffmpeg.exe` di samping `Suarakata.exe` (atau sub-folder
> `ffmpeg\`) agar aplikasi mandiri saat dipindah ke komputer lain.

## Cara pakai (GUI)

1. Jalankan `Suarakata.exe`.
2. Klik **Telusuri...** atau seret file audio ke jendela.
3. Pilih **Model** dan **Bahasa** (default: Small, Indonesia).
4. (Opsional) centang **Sertakan waktu**.
5. Klik **Transkripsi**. Saat pertama kali, model akan diunduh ke folder `models\`.
6. Setelah selesai, klik **Simpan .txt** atau **Salin**.

## Cara pakai (CLI)

```powershell
Suarakata.exe "<file audio>" [model] [bahasa] [output.txt]
Suarakata.exe --models                 # daftar model beserta statusnya
Suarakata.exe --download <model>       # unduh model (otomatis lanjut bila ada .part)
```

Contoh:

```powershell
Suarakata.exe "C:
ekaman\pesan.ogg" small id "C:
ekaman\pesan.txt"
Suarakata.exe --download medium
```

- `model`: `tiny` | `base` | `small` | `medium` | `large-v3` (default `small`)
- `bahasa`: kode ISO seperti `id`, `en`, `ms`, `auto` (default `id`)
- `output.txt`: opsional, default `<file audio>.stt.txt`

Menekan `Ctrl+C` saat mengunduh hanya menjeda: berkas `.part` dipertahankan dan perintah
`--download` berikutnya melanjutkan dari titik terakhir.

## Model

| Model     | Ukuran unduh | Akurasi | Kecepatan (CPU) |
|-----------|--------------|---------|-----------------|
| Tiny      | ~74 MB       | Rendah  | Sangat cepat    |
| Base      | ~141 MB      | Sedang- | Cepat           |
| Small     | ~465 MB      | Sedang+ | Menengah        |
| Medium    | ~1,43 GB     | Tinggi  | Lambat          |
| Large-v3  | ~2,88 GB     | Terbaik | Paling lambat   |

Model disimpan di folder `models\` di samping executable dan diunduh dari Hugging Face
(`https://huggingface.co/ggerganov/whisper.cpp`).

### Mengunduh model (Kelola Model)

Klik **Kelola Model** di header. Tiap baris punya status (Terpasang / Belum diunduh /
Belum selesai) dan tombol:

- **Unduh** memulai unduhan; saat berjalan tombol berubah jadi **Jeda**.
- **Jeda** menghentikan aliran data tetapi menyimpan `models\ggml-*.bin.part`.
  Tombol berubah jadi **Lanjutkan** dan unduhan diteruskan dari byte terakhir.
- **Batal** menghentikan unduhan sekaligus membuang berkas `.part`.
- Menu **roda gigi** berisi: *Unduh lewat browser*, *Impor berkas model (.bin)*,
  *Salin tautan unduhan*, *Hapus unduhan tertunda*, dan *Hapus model dari disk*.

Menutup jendela saat unduhan berjalan akan menjeda (bukan membatalkan), sehingga
unduhan bisa dilanjutkan pada sesi berikutnya.

### Unduh lewat browser

Bila jaringan tidak stabil atau ingin memakai pengelola unduhan sendiri, pilih
**Unduh lewat browser** pada menu roda gigi. Tautan model terbuka di peramban;
setelah berkas `ggml-*.bin` selesai diunduh di sana, kembali ke Kelola Model dan pilih
**Impor berkas model (.bin)**. Berkas disalin ke folder `models\` dengan nama yang benar,
dan ukurannya diperiksa agar berkas yang belum lengkap tidak terpakai.

## Bahasa yang tersedia di GUI

Auto-deteksi, Indonesia (`id`), Banjar (via Indonesia → `id`), Inggris (`en`),
Melayu (`ms`), Jawa (`jw`), Sunda (`su`), Arab (`ar`), Mandarin (`zh`). Mode CLI
menerima kode bahasa apa pun yang didukung Whisper.

> Catatan: Whisper tidak memiliki kode bahasa Banjar tersendiri. Opsi "Banjar
> (via Indonesia)" memakai model Indonesia (`id`) sebagai pendekatan terbaik.

## Struktur proyek

```
Suarakata/
├─ Suarakata.sln
├─ Suarakata.csproj
├─ app.manifest
├─ Program.cs            # Entry point + mode CLI
├─ Transcriber.cs        # Pipeline: ffmpeg + Whisper.net
├─ ModelManager.cs       # Katalog model + pengunduh jeda/lanjut (HTTP Range)
├─ ModelManagerForm.cs   # Jendela Kelola Model
├─ Ui.cs                 # Tema terang/gelap, preferensi, kontrol custom
└─ MainForm.cs           # UI utama
```

## Troubleshooting

- **"ffmpeg tidak ditemukan"** → letakkan `ffmpeg.exe` di folder aplikasi atau PATH.
- **Gagal load native / crash saat transkripsi** → pasang VC++ Redistributable x64,
  pastikan CPU mendukung AVX2. Untuk CPU tanpa AVX, ganti paket runtime ke
  `Whisper.net.Runtime.NoAvx`.
- **Unduhan model terputus** → buka Kelola Model dan klik **Lanjutkan**; unduhan
  diteruskan dari byte terakhir, tidak mengulang dari nol.
- **Unduhan sangat lambat** → pakai **Unduh lewat browser** lalu **Impor berkas model**,
  atau salin `ggml-*.bin` secara manual ke folder `models\`.
- **Hasil kurang akurat** → gunakan model lebih besar (`medium` / `large-v3`).

## Distribusi (installer)

Installer dibuat dengan [Inno Setup 6](https://jrsoftware.org/isdl.php):

```powershell
powershell -ExecutionPolicy Bypass -File .uild-release.ps1
```

Skrip ini melakukan build Release, menyalin `ffmpeg.exe` ke `bin\Release`, lalu memanggil
`ISCC.exe` bila Inno Setup terpasang (dicari di Program Files maupun
`%LocalAppData%\Programs\Inno Setup 6`). Hasilnya:

```
installer\Output\Suarakata-Setup-1.2.1.exe
```

Untuk mengompilasi installer saja:

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\installer\Suarakata.iss
```

Isi paket: `Suarakata.exe`, `Suarakata.exe.config`, `ffmpeg.exe`, seluruh `*.dll`,
`runtimes\win-x64\`, `README.md`, dan `LICENSE.txt`. Runtime Linux/macOS serta
win-x86/win-arm64 dari paket NuGet sengaja tidak disertakan.

> Ukuran installer sekitar 59 MB dan hampir seluruhnya berasal dari `ffmpeg.exe`
> (build statis penuh). Bila ingin paket lebih kecil, ganti `bin\Releasefmpeg.exe`
> dengan build "essentials" sebelum mengompilasi installer.

### Rilis otomatis

Setiap push ke `main` memicu alur kerja [`.github/workflows/release.yml`](.github/workflows/release.yml)
di GitHub Actions. Alur ini membaca `<Version>` dari `Suarakata.csproj`, dan bila tag
`v<versi>` belum ada, ia membangun aplikasi, mengunduh ffmpeg build LGPL, mengompilasi
installer, lalu menerbitkan rilis berisi dua berkas:

- `Suarakata-Setup-<versi>.exe` - installer
- `Suarakata-<versi>-portable.zip` - versi portable

Bila tag untuk versi itu sudah ada, alur berhenti tanpa membuat apa pun, sehingga push
biasa tidak menghasilkan rilis ganda.

Naikkan versi lewat skrip supaya nomor di `Suarakata.csproj` dan `installer\Suarakata.iss`
tetap seragam (alur rilis menolak versi yang tidak sinkron):

```powershell
powershell -ExecutionPolicy Bypass -File .\set-version.ps1 1.3.0
git commit -am "rilis 1.3.0"
git push
```

### Lokasi model setelah dipasang

Model disimpan di folder `models\` di samping executable bila folder itu bisa ditulis
(mode portable). Karena installer memasang aplikasi ke Program Files yang hanya bisa
dibaca, model otomatis pindah ke:

```
%LocalAppData%\Suarakata\models
```

Folder yang sedang dipakai selalu ditampilkan di bagian bawah jendela Kelola Model.
Model yang diletakkan manual di folder aplikasi tetap ikut terbaca.

Alternatif tanpa installer: **zip seluruh isi `bin\Release`** (termasuk `Suarakata.exe`,
`ffmpeg.exe`, folder `runtimes\`, dan `*.dll`) lalu ekstrak di komputer tujuan. Dalam
mode ini model tersimpan di `models\` di samping executable.

> Catatan lisensi: `ffmpeg.exe` yang dibundel mengikuti lisensi build-nya
> (LGPL/GPL). Sertakan pemberitahuan lisensi ffmpeg bila mendistribusikan.

## Lisensi

Whisper.net dan whisper.cpp berlisensi MIT. ffmpeg berlisensi LGPL/GPL sesuai build.
