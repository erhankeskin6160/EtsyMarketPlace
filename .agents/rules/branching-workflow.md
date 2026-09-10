# 🚨 Görev ve Dal (Branch) Yönetimi Kuralı (KESİNLİKLE ZORUNLU)

Her geliştirme görevi, hata düzeltmesi veya yeni özellik (task, feature, bugfix) için aşağıdaki iş akışı **İSTİSNASIZ** uygulanacaktır:

---

### 1. Yeni Branch Açma (Adım 1)
- Herhangi bir kod yazmadan veya göreve başlamadan önce doğrudan `development` üzerinde çalışılmaz!
- Güncel `development` dalından yeni bir görev dalı (branch) açılır:
  - Yeni özellikler için: `feature/<kısa-açıklayıcı-ad>` (Örn: `feature/ai-background-editor`)
  - Hata düzeltmeleri için: `fix/<kısa-ad>` veya `bugfix/<kısa-ad>`
- Tüm geliştirme, kodlama ve yerel testler bu özel dalda gerçekleştirilir.

---

### 2. Feature/Fix Dalında Commit (Adım 2)
- Görev tamamlandığında ve derleme/testler doğrulandığında (`dotnet build`, `dotnet test`), tüm değişiklikler bu branch üzerinde temiz ve açıklayıcı bir commit mesajı ile commit'lenir:
  `git add -A`
  `git commit -m "feat/fix: ..."`

---

### 3. development Dalına Merge Etme (Adım 3)
- `development` dalına geçilir:
  `git checkout development`
- Görev dalı `development` dalına merge edilir:
  `git merge <feature/fix-branch-adi>`

---

### 4. Otomatik Uzak Repoya Push (Adım 4)
- Merge tamamlandıktan sonra kullanıcıdan ek onay beklenmeden doğrudan uzak depoya push yapılır:
  `git push origin development`
- İşlem sonucu kullanıcıya açık ve şeffaf şekilde raporlanır.
