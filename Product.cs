namespace Notissimus_test
{
    internal class Product
    {
        /// <summary>
        /// Селектор имени
        /// </summary>
        public static readonly string NameSelector = "h1.product-page__header";
        /// <summary>
        /// Селектор цены
        /// </summary>
        public static readonly string PriceSelector = "div.product-buy__price-wrapper div[data-autotest-target-id='product-item-buy-block-price']";
        /// <summary>
        /// Селектор цены без скидки
        /// </summary>
        public static readonly string OldPriceSelector = "div.product-buy__old-price";
        /// <summary>
        /// Селектор рейтинга товара
        /// </summary>
        public static readonly string RatingSelector = "p.rating-stars__value";
        /// <summary>
        /// Селектор объема тары
        /// </summary>
        public static readonly string VolumeSelector = "dd.product-brief__value a[href*='volume']";
        /// <summary>
        /// Селектор артикула
        /// </summary>
        public static readonly string ArticleSelector = "span.product-page__article";
        /// <summary>
        /// Селектор города товара
        /// </summary>
        public static readonly string RegionSelector = "button.location__current";
        /// <summary>
        /// Селектор ссылки на товар
        /// </summary>
        public static readonly string UrlSelector = "link[rel='canonical']";
        /// <summary>
        /// Селектор ссылок на изображения
        /// </summary>
        public static readonly string PicturesUrlsSelector = "picture.product-slider__slide-picture source[media*='(min-width: 1024px)']:nth-child(1)";
        /// <summary>
        /// Наименование товара
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// Цена товара
        /// </summary>
        public int? Price { get; set; }
        /// <summary>
        /// Цена без скидки
        /// </summary>
        public int? OldPrice { get; set; }
        /// <summary>
        /// Рейтинг товара
        /// </summary>
        public double? Rating { get; set; }
        /// <summary>
        /// Объуем тары
        /// </summary>
        public string? Volume { get; set; }
        /// <summary>
        /// Артикул товара
        /// </summary>
        public int? Article { get; set; }
        /// <summary>
        /// Город
        /// </summary>
        public string? Region { get; set; }
        /// <summary>
        /// Ссылка на товар
        /// </summary>
        public string? Url { get; set; }
        /// <summary>
        /// Список ссылок на изобажения
        /// </summary>
        public List<string>? PicturesUrls { get; set; } = [];
    }
}
