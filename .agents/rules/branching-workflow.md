# Görev ve Dal (Branch) Yönetimi Kuralı

Her geliştirme görevi (task) için aşağıdaki iş akışı zorunludur:

1. **Yeni Branch Açma**:
   - Her task'e başlamadan önce en güncel `development` dalı çekilir (`git checkout development && git pull origin development`).
   - Task için amaca uygun yeni bir dal açılır (Örn: `feature/<task-adi>` veya `fix/<task-adi>`).
   - Geliştirmeler ve testler bu özel dalda yapılır.

2. **development Dalına Merge Etme ve Otomatik Push**:
   - Task başarıyla tamamlanıp doğrulandıktan (derleme, testler vs.) sonra değişiklikler commit'lenir.
   - `development` dalına geçilerek ilgili task dalı `development` branch'ine merge edilir.
   - **Kullanıcıdan onay beklemeden OTOMATİK OLARAK uzak repoya push edilir**:
     `git push origin development`
   - İşlemin tamamlandığı kullanıcıya raporlanır.
