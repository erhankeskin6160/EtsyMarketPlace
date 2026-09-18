# Clean Architecture Katman Kuralı

Projede **Clean Architecture (Temiz Mimari)** prensipleri katı bir şekilde uygulanır:

---

### 1. Katman Hiyerarşisi ve Bağımlılık Yönü

Bağımlılıklar daima dış katmandan iç katmana doğrudur:
`Presentation (SimilarProductsWinForms) -> Infrastructure -> Application -> Domain`

1. **Domain (`src/EtsyMarketPlace.Domain`):**
   - Çekirdek iş varlıkları (Entities), Value Objects, Domain servisleri ve en temel arayüzler.
   - Hiçbir dış kütüphaneye veya üst katmana bağımlılığı yoktur.

2. **Application (`src/EtsyMarketPlace.Application`):**
   - İş mantığı, kullanım senaryoları (Use Cases), DTO'lar, Prompt oluşturucular, hesaplayıcılar ve Repository arayüzleri (`I...Repository`).
   - UI veya veri tabanı teknolojilerine (SQLite, WinForms vb.) doğrudan bağımlı olamaz.

3. **Infrastructure (`src/EtsyMarketPlace.Infrastructure`):**
   - Application katmanında tanımlanan arayüzlerin somut implementasyonları (SQLite veritabanı erişimi, dosya I/O, dış API adaptörleri).

4. **Presentation (`SimilarProductsWinForms`):**
   - Kullanıcı arayüzü (WinForms Formları, UserControl'ler, Dialog'lar ve `UiStyle.cs`).
   - İş mantığını doğrudan form içine gömmek yerine Application katmanındaki servisleri tüketir.

---

### 2. Geliştirme Kuralları
- Yeni bir iş kuralı, analiz veya hesaplama eklenirken ilgili mantık **Application** katmanında servis veya model olarak yazılır; ardından Presentation katmanından çağrılır.
- Veri tabanı ve kalıcı depolama arayüzleri Application katmanında tanımlanır, implementasyonları **Infrastructure** veya ilgili servis katmanında yapılır.
- Tüm yeni servis ve senaryolar için `tests/EtsyMarketPlace.Application.Tests` altında birim testleri yazılır ve `dotnet test` ile 0 hata doğrulanır.
