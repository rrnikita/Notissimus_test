using AngleSharp;
using AngleSharp.Dom;
using Newtonsoft.Json;
using Notissimus_test;
using System.Text;
using System.Threading.Tasks.Dataflow;

internal class Program
{
    const string firstPageUrl = "https://simplewine.ru/catalog/shampanskoe_i_igristoe_vino/"; // Cсылка на первую страницу каталога

    const int maxTryCount = 3; // Ограниченипе количества попыток отправки запросов для httpClient

    static Random random = new();  // Объект Random для перерывами между запросами httpClient

    const string region = "Sochi"; // Выбор города для выставления куки в хедерах запросов

    static object productListLocker = new();

    static object pageIndexListLocker = new();

    static List<int> pageIndexList = [];
    private static async Task Main(string[] args)
    {
        List<Product> products = [];

        Console.OutputEncoding = Encoding.UTF8;

        IConfiguration config = Configuration.Default; // Создание конфигурации AngleSharp
        using (HttpClient httpClient = new()) // Создание http-клиента для загрузки страниц
        using (IBrowsingContext context = BrowsingContext.New(config)) // Создание браузера AngleSharp
        {
            #region HttpClientConfiguration
            httpClient.BaseAddress = new Uri("https://simplewine.ru"); // Установка адреса по умолчанию для http запросов
                                                                       // Установка хедеров для http запросов
            httpClient.DefaultRequestHeaders.Add("UserAgent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Host", httpClient.BaseAddress.Host);
            httpClient.DefaultRequestHeaders.Add("AcceptLanguage", "ru-RU, ru;q=0.9, en-US;q=0.8, en;q=0.7");
            httpClient.DefaultRequestHeaders.Add("AcceptEncoding", "gzip, deflate, br, zstd");
            if (region != String.Empty)
            {
                // Если переменная region имеет значение Sochi, в хедеры добавятся куки с кодом города Сочи и парсинг будет происходить по этому городу
                httpClient.DefaultRequestHeaders.Add("Cookie", "isCityDetect=1;BITRIX_SM_cityCode=SOCHI; BITRIX_SM_PK=SOCHI_USER_SORT_our_choice_experiment_ab_side_filters_original");
            }
            #endregion

            int threadCount = 3; //Ограничение количества потоков
            int pageCount = await GetPagesCountAsync(httpClient, context); // Получение количества страниц каталога
            pageIndexList.AddRange(Enumerable.Range(1, pageCount));

            while (pageIndexList.Count > 0)
            {
                ActionBlock<int> actionBlock = new(async (pageIndex) =>
                {
                    try
                    {
                        var pageProducts = await GetProductsListAsync(httpClient, context, pageIndex).ConfigureAwait(false);

                        lock (productListLocker)
                        {
                            products.AddRange(pageProducts);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Не удалось получить страницу {pageIndex}. {ex.Message}");
                    }
                }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = threadCount });

                for (int i = 1; i <= pageIndexList.Count; i++)
                {
                    await actionBlock.SendAsync(i).ConfigureAwait(false);
                }

                actionBlock.Complete();
                await actionBlock.Completion.ConfigureAwait(false);
            }

            string fileName = string.Empty;

            if (region != String.Empty)
            {
                fileName = @"Cache\prodcutsJson-Sochi.json";
            }
            else
            {
                fileName = @"Cache\prodcutsJson-Moscow.json";
            }

            string productsJson = JsonConvert.SerializeObject(products, Formatting.Indented);
            File.WriteAllText(fileName, productsJson);
        }
    }

    /// <summary>
    /// Получает список товаров
    /// </summary>
    /// <param name="httpClient">Объект HttpClient для отправки http запросов</param>
    /// <param name="AngleSharpBrowserContext">Эмитация браузера AngleSharp</param>
    /// <param name="pageIndex">Индекс страницы, с которой получается список товаров</param>
    /// <returns>Список товаров</returns>
    static async Task<List<Product>> GetProductsListAsync(HttpClient httpClient, IBrowsingContext AngleSharpBrowserContext, int pageIndex)
    {
        List<Product> products = [];

        const string productElementSelector = "a.snippet-name.js-dy-slot-click"; // Селектор ссылки на элемент товара

        string catalogPageUrl = $"{firstPageUrl}page{pageIndex}"; // Формирование ссылки на страницу каталога

        string catalogPageHtmlContent = await GetPageHtmlAsync(httpClient, catalogPageUrl).ConfigureAwait(false); // Запрос для получения HTML страницы каталога

        lock (pageIndexListLocker)
        {
            pageIndexList.Remove(pageIndex);
        }

        IDocument catalogPageHtml = await AngleSharpBrowserContext.OpenAsync(req => req.Content(catalogPageHtmlContent)).ConfigureAwait(false); // Передача HTML в документ AngleSharp

        var productUrlElements = catalogPageHtml.QuerySelectorAll(productElementSelector); // Получение коллекции всех элементов с ссылками на товары с текущей страницы каталога

        // Итерация по всем элементам в коллекции 
        foreach (var productUrlElement in productUrlElements)
        {
            string productUrl = productUrlElement?.GetAttribute("href"); // Получение ссылки на товар из текущего элемента

            string productPageHtml = await GetPageHtmlAsync(httpClient, productUrl).ConfigureAwait(false);

            Product product = new Product();
            product = await ParseProductsHtmlAsync(productPageHtml, AngleSharpBrowserContext, product).ConfigureAwait(false);
            products.Add(product);

            await Task.Delay(random.Next(1500, 4500));
        }

        return products;
    }
    /// <summary>
    /// Получает HTML страницы по переданной URL
    /// </summary>
    /// <param name="httpClient">Объект HttpClient для отправки http запросов</param>
    /// <param name="Url">Ссылка на страницу, с которой требуется получить HTML</param>
    /// <returns>Строку HTML страницы</returns>
    static async Task<string> GetPageHtmlAsync(HttpClient httpClient, string Url)
    {
        int tries = 0;
        string htmlContent = string.Empty;
        while (true)
        {
            tries++;
            try
            {
                htmlContent = await httpClient.GetStringAsync(Url).ConfigureAwait(false);
                return htmlContent;
            }
            catch (Exception ex)
            {
                if (tries == maxTryCount - 1)
                {
                    throw;
                }
            }
        }
    }
    /// <summary>
    /// Парсит переданный HTML и возвращает объект Product
    /// </summary>
    /// <param name="productPageHtml">HTML страницы, который нужно парсить</param>
    /// <param name="AngleSharpBrowserContext">Эмитация браузера AngleSharp</param>
    /// <param name="product">Получает объект класса Product для заполнения его свойств</param>
    /// <returns>объект Product</returns>
    static async Task<Product> ParseProductsHtmlAsync(string productPageHtml, IBrowsingContext AngleSharpBrowserContext, Product product)
    {
        IDocument productPageHtmlContent = await AngleSharpBrowserContext.OpenAsync(req => req.Content(productPageHtml)).ConfigureAwait(false); // Передача HTML в документ AngleSharp

        // Парсинг наименования товара
        product.Name = productPageHtmlContent.QuerySelector(Product.NameSelector)?.Text().Trim();

        // Парсинг цены товара
        var productPriceElement = productPageHtmlContent.QuerySelector(Product.PriceSelector);
        var productPriceElementText = productPriceElement.ChildNodes.OfType<IText>().Select(x => x.Text()).FirstOrDefault();
        productPriceElementText = string.Concat(productPriceElementText.Where(char.IsDigit));
        product.Price = int.Parse(productPriceElementText);

        // Парсинг старой цены товара
        string? productOldPriceBuffer = productPageHtmlContent.QuerySelector(Product.OldPriceSelector)?.Text().Trim();
        if (productOldPriceBuffer != null)
        {
            productOldPriceBuffer = string.Concat(productOldPriceBuffer.Where(char.IsDigit));
            product.OldPrice = int.Parse(productOldPriceBuffer);
        }
        else
        {
            product.OldPrice = product.Price;
        }

        // Парсинг рейтинга товара
        string productRatingBuffer = productPageHtmlContent.QuerySelector(Product.RatingSelector)?.Text().Trim();
        product.Rating = double.Parse(productRatingBuffer, System.Globalization.CultureInfo.InvariantCulture);

        // Парсинг объема товара
        product.Volume = productPageHtmlContent.QuerySelector(Product.VolumeSelector)?.Text().Trim();

        // Парсинг артикула товара
        string productArticleBuffer = productPageHtmlContent.QuerySelector(Product.ArticleSelector)?.Text().Trim();
        productArticleBuffer = string.Concat(productArticleBuffer.Where(char.IsDigit));
        product.Article = int.Parse(productArticleBuffer);

        // Парсинг региона товара
        product.Region = productPageHtmlContent.QuerySelector(Product.RegionSelector)?.Text().Trim();

        // Парсинг ссылки на товар
        product.Url = productPageHtmlContent.QuerySelector(Product.UrlSelector).GetAttribute("href");

        // Парсинг ссылок на изображения
        var picturesUrlsElements = productPageHtmlContent.QuerySelectorAll(Product.PicturesUrlsSelector);
        foreach (var pictureUrlElement in picturesUrlsElements)
        {
            string pictureUrlBuffer = pictureUrlElement.GetAttribute("srcset");
            int pos1 = pictureUrlBuffer.IndexOf("1x,") + 3;
            int pos2 = pictureUrlBuffer.IndexOf("2x");
            string result = pictureUrlBuffer.Substring(pos1, pos2 - pos1).Trim();
            product.PicturesUrls.Add(result);
        }

        product.PicturesUrls.Distinct();

        Console.WriteLine($"Название: {product.Name} \n Цена: {product.Price}, Старая цена: {product.OldPrice}, Рейтинг: {product.Rating}, Объём: {product.Volume}, Артикул: {product.Article}, Регион: {product.Region}\n" +
                          $"Ссылка: {product.Url}");

        foreach (var productPictureUrl in product.PicturesUrls)
        {
            Console.WriteLine($"Ссылка на картинку: {productPictureUrl}");
        }

        Console.WriteLine();
        return product;
    }
    /// <summary>
    /// Получает количество страниц в каталоге
    /// </summary>
    /// <param name="httpClient">Объект HttpClient для отправки http запросов</param>
    /// <param name="AngleSharpBrowserContext">Эмитация браузера AngleSharp</param>
    /// <returns>Количество страниц</returns>
    static async Task<int> GetPagesCountAsync(HttpClient httpClient, IBrowsingContext AngleSharpBrowserContext)
    {
        int pagesCount = 0;

        string catalogPageHtmlContent = await GetPageHtmlAsync(httpClient, firstPageUrl).ConfigureAwait(false);

        IDocument catalogPageHtml = await AngleSharpBrowserContext.OpenAsync(req => req.Content(catalogPageHtmlContent)).ConfigureAwait(false);

        string? pagesCountBuffer = catalogPageHtml.QuerySelector("div.pagination__navigation a:nth-last-child(2)")?.Text();

        pagesCount = int.Parse(pagesCountBuffer);

        return pagesCount;
    }
}