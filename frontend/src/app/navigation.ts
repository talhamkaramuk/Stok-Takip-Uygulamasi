import type { TabKey } from "../shared/types/ui";

export const tabMeta: Record<TabKey, { title: string; description: string }> = {
  dashboard: {
    title: "Ana Sayfa",
    description: "Operasyon hacmini, stok akışını ve kritik işleri canlı verilerle izleyin."
  },
  products: {
    title: "Ürünler",
    description: "Ürün kataloğunu, barkodları ve kritik stok seviyelerini yönetin."
  },
  categories: {
    title: "Kategoriler",
    description: "Ürün gruplarını düzenleyin ve katalog kırılımlarını takip edin."
  },
  customers: {
    title: "Müşteriler",
    description: "Satış, sevkiyat ve iade süreçlerinde kullanılan müşteri kartlarını yönetin."
  },
  suppliers: {
    title: "Tedarikçiler",
    description: "Alım taleplerinde kullanılan tedarikçi kartlarını ve iletişim bilgilerini yönetin."
  },
  orders: {
    title: "Siparişler",
    description: "Müşteri siparişlerini, hazırlanma durumunu ve sevkiyata çıkan ürünleri takip edin."
  },
  purchase: {
    title: "Alım Talep",
    description: "Tedarik taleplerini oluşturun, onaylayın ve teslim alındığında stoğa işleyin."
  },
  shipments: {
    title: "Sevkiyat",
    description: "Müşteri gönderilerini oluşturun ve sevkiyatla stok çıkışını kayıt altına alın."
  },
  returns: {
    title: "İadeler",
    description: "İade alınan ürünleri kaydedin ve uygun depoya stok girişi oluşturun."
  },
  warehouses: {
    title: "Depolar",
    description: "Şube, depo ve raf lokasyonlarına göre stok bakiyelerini ve transferleri yönetin."
  },
  stock: {
    title: "Stok Hareketleri",
    description: "Giriş, çıkış, düzeltme ve sayım kaynaklı stok hareketlerini kaydedin."
  },
  count: {
    title: "Sayım",
    description: "Barkodla sayım yapın, farkları görün ve stokla mutabakat sağlayın."
  },
  users: {
    title: "Kullanıcılar",
    description: "Ekip üyelerini ve rol bazlı erişimleri yönetin."
  },
  audit: {
    title: "Denetim",
    description: "Audit loglarını, request metriklerini ve kritik operasyon sinyallerini izleyin."
  },
  profile: {
    title: "Profilim",
    description: "Oturumdaki kullanıcı, rol ve çalışma alanı bilgilerini görüntüleyin."
  },
  reports: {
    title: "Raporlar",
    description: "Stok, kritik seviye, hareket ve sayım farkı raporlarını dışa aktarın."
  }
};
