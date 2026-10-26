namespace GestionPedidos.Infrastructure.Persistence.Seed;

/// <summary>Filtro de ejemplo con sus opciones.</summary>
internal sealed record FiltroEj(string Nombre, string[] Opciones);

/// <summary>
/// Producto de ejemplo. <c>Detalle</c> fija algunas opciones (<c>"Color=Negro;Talla=M"</c>); las que no se indican las
/// completa el sembrador de forma determinista, para que todos los productos queden descritos.
/// </summary>
internal sealed record ProductoEj(string Nombre, string Descripcion, decimal Precio, int Stock, string Detalle, bool Activo = true);

/// <summary>Subcategoría de ejemplo.</summary>
internal sealed record SubcategoriaEj(string Codigo, string Nombre, string Descripcion, FiltroEj[] Filtros, ProductoEj[] Productos);

/// <summary>Categoría de ejemplo.</summary>
internal sealed record CategoriaEj(string Codigo, string Nombre, string Descripcion, SubcategoriaEj[] Subcategorias);

/// <summary>
/// Datos realistas del catálogo para desarrollo y demostraciones ([CatalogoProductos.md §11]): 6 categorías con 3 a 5
/// subcategorías, sus filtros con opciones y de 2 a 3 productos por subcategoría (precios en dólares), salvo Celulares (25) y Laptops (35),
/// que llevan marcas reales (Samsung, Motorola, HP, ASUS…) para probar filtros, paginación y rendimiento con volumen.
/// </summary>
// DÓNDE EDITAR: aquí se cambian o agregan productos de ejemplo. El código de cada producto sale de su posición en la lista (CAT-SUB-001, 002…):
// agreguen siempre AL FINAL de la subcategoría para no correr los números ni el nombre de las imágenes (seed/imagenes/productos/CODIGO.jpg).
// Quién lo ejecuta: DatosDeEjemplo.cs. Mapa del repositorio: docs/Equipo/DondeEstaCadaCosa.md
internal static class CatalogoDeEjemplo
{
    private static FiltroEj F(string nombre, params string[] opciones) => new(nombre, opciones);
    private static ProductoEj P(string n, string d, decimal precio, int stock, string detalle, bool activo = true) => new(n, d, precio, stock, detalle, activo);

    private static readonly string[] Colores = { "Negro", "Blanco", "Azul", "Rojo", "Gris", "Verde", "Beige" };
    private static readonly string[] TallasRopa = { "S", "M", "L", "XL" };
    private static readonly string[] Clima = { "Cálido", "Fresco", "Frío", "Mixto" };

    /// <summary>Categoría que se deja inactiva y vacía a propósito, para probar la eliminación definitiva.</summary>
    public const string CategoriaInactiva = "Temporada pasada";
    /// <summary>Código de la categoría inactiva de ejemplo.</summary>
    public const string CodigoCategoriaInactiva = "TPA";
    /// <summary>Opción que se deja inactiva a propósito (Color de camisas).</summary>
    public const string OpcionInactiva = "Fucsia";

    /// <summary>Las categorías de ejemplo.</summary>
    public static readonly CategoriaEj[] Categorias =
    {
        new("ROP", "Ropa", "Prendas de vestir para toda ocasión y clima", new[]
        {
            new SubcategoriaEj("CAM", "Camisas", "Camisas y camisetas para hombre y mujer",
                new[] { F("Clima", Clima), F("Color", "Blanco", "Azul", "Celeste", "Negro", "Rojo", "Fucsia"), F("Material", "Algodón", "Lino", "Poliéster"),
                        F("Marca", "Polo Club", "Arrow Street", "Basement"), F("Talla", TallasRopa) },
                new[]
                {
                    P("Camisa Oxford manga larga", "Camisa de corte regular con cuello abotonado, ideal para la oficina.", 24.99m, 40, "Clima=Mixto;Color=Celeste;Material=Algodón;Marca=Arrow Street;Talla=M"),
                    P("Camisa de lino manga corta", "Tela ligera y fresca para los días de calor.", 19.50m, 55, "Clima=Cálido;Color=Blanco;Material=Lino;Marca=Basement;Talla=L"),
                    P("Camiseta polo clásica", "Polo de piqué con bordado en el pecho.", 16.00m, 80, "Clima=Cálido;Color=Azul;Material=Algodón;Marca=Polo Club;Talla=M"),
                }),
            new SubcategoriaEj("PAN", "Pantalones", "Jeans, de vestir y casuales",
                new[] { F("Clima", Clima), F("Color", "Negro", "Azul", "Gris", "Beige", "Verde"), F("Material", "Mezclilla", "Gabardina", "Algodón"),
                        F("Marca", "Denim Co", "Urban Fit", "Classic Wear"), F("Talla", "28", "30", "32", "34", "36") },
                new[]
                {
                    P("Jeans slim fit", "Mezclilla elástica de corte ajustado.", 34.99m, 60, "Clima=Mixto;Color=Azul;Material=Mezclilla;Marca=Denim Co;Talla=32"),
                    P("Pantalón de vestir de gabardina", "Corte recto, perfecto para ocasiones formales.", 29.99m, 35, "Clima=Fresco;Color=Gris;Material=Gabardina;Marca=Classic Wear;Talla=34"),
                    P("Pantalón cargo con bolsillos", "Cómodo y resistente para el día a día.", 27.50m, 45, "Clima=Mixto;Color=Verde;Material=Algodón;Marca=Urban Fit;Talla=30"),
                }),
            new SubcategoriaEj("CAL", "Calzado", "Tenis, botas y sandalias",
                new[] { F("Clima", Clima), F("Color", Colores), F("Material", "Cuero", "Lona", "Sintético", "Malla"),
                        F("Marca", "Stride", "Urban Fit", "TrailOne"), F("Talla", "38", "39", "40", "41", "42", "43") },
                new[]
                {
                    P("Tenis running ligeros", "Suela amortiguada y malla transpirable.", 54.90m, 38, "Clima=Cálido;Color=Negro;Material=Malla;Marca=Stride;Talla=42"),
                    P("Botas de cuero", "Botas resistentes con suela antideslizante.", 79.00m, 20, "Clima=Frío;Color=Negro;Material=Cuero;Marca=TrailOne;Talla=41"),
                    P("Sandalias casuales", "Sandalias cómodas para el verano.", 22.00m, 50, "Clima=Cálido;Color=Beige;Material=Sintético;Marca=Urban Fit;Talla=39"),
                }),
            new SubcategoriaEj("CHA", "Chaquetas y abrigos", "Para días frescos, fríos o de lluvia",
                new[] { F("Clima", "Frío", "Fresco", "Lluvia"), F("Color", Colores), F("Material", "Poliéster", "Nylon", "Lana", "Cuero"),
                        F("Marca", "TrailOne", "Classic Wear", "Basement"), F("Talla", TallasRopa) },
                new[]
                {
                    P("Chaqueta rompevientos impermeable", "Ligera y plegable, con capucha ajustable.", 39.99m, 42, "Clima=Lluvia;Color=Azul;Material=Nylon;Marca=TrailOne;Talla=L"),
                    P("Abrigo largo de lana", "Abrigo elegante para las noches frías.", 89.00m, 12, "Clima=Frío;Color=Gris;Material=Lana;Marca=Classic Wear;Talla=M"),
                }),
            new SubcategoriaEj("COM", "Complementos", "Cinturones, gorras, bufandas y más",
                new[] { F("Color", Colores), F("Material", "Cuero", "Tela", "Lana", "Metal"), F("Marca", "Classic Wear", "Basement", "Urban Fit") },
                new[]
                {
                    P("Cinturón de cuero", "Cinturón con hebilla metálica clásica.", 18.00m, 70, "Color=Negro;Material=Cuero;Marca=Classic Wear"),
                    P("Gorra deportiva ajustable", "Gorra de tela transpirable con visera curva.", 12.50m, 90, "Color=Rojo;Material=Tela;Marca=Urban Fit"),
                    P("Bufanda de lana", "Suave y abrigadora. Fuera de temporada.", 15.00m, 30, "Color=Gris;Material=Lana;Marca=Basement", activo: false),
                }),
        }),

        new("TEC", "Tecnología", "Celulares, computadoras, audio y accesorios", new[]
        {
            // 25 teléfonos de 12 marcas
            new SubcategoriaEj("CEL", "Celulares", "Teléfonos inteligentes",
                new[] { F("Marca", "Samsung", "Motorola", "Xiaomi", "Apple", "Google", "OPPO", "realme", "Nokia", "OnePlus", "Honor", "Sony", "Huawei"),
                        F("Almacenamiento", "64 GB", "128 GB", "256 GB", "512 GB"),
                        F("Memoria RAM", "4 GB", "6 GB", "8 GB", "12 GB"),
                        F("Color", "Negro", "Azul", "Plata", "Blanco", "Verde", "Morado", "Dorado"),
                        F("Pantalla", "6.1\"", "6.4\"", "6.6\"", "6.7\"", "6.8\"") },
                new[]
                {
                    P("Samsung Galaxy S24 Ultra", "Cámara de 200 MP, lápiz S Pen y pantalla enorme.", 1299m, 8, "Marca=Samsung;Almacenamiento=256 GB;Memoria RAM=12 GB;Color=Plata;Pantalla=6.8\""),
                    P("Samsung Galaxy S23", "Compacto, rápido y con excelente cámara.", 799m, 10, "Marca=Samsung;Almacenamiento=256 GB;Memoria RAM=8 GB;Color=Dorado;Pantalla=6.1\""),
                    P("Samsung Galaxy A55", "Gama media con pantalla AMOLED y marco de metal.", 449m, 22, "Marca=Samsung;Almacenamiento=256 GB;Memoria RAM=8 GB;Color=Blanco;Pantalla=6.6\""),
                    P("Samsung Galaxy A35", "Pantalla fluida y batería de larga duración.", 349m, 26, "Marca=Samsung;Almacenamiento=128 GB;Memoria RAM=6 GB;Color=Blanco;Pantalla=6.6\""),
                    P("Samsung Galaxy A15", "Básico y confiable para el día a día.", 169m, 35, "Marca=Samsung;Almacenamiento=128 GB;Memoria RAM=4 GB;Color=Azul;Pantalla=6.6\""),
                    P("Motorola Moto G54 5G", "5G accesible con batería para dos días.", 199m, 30, "Marca=Motorola;Almacenamiento=128 GB;Memoria RAM=6 GB;Color=Azul;Pantalla=6.6\""),
                    P("Motorola Edge 40 Neo", "Pantalla curva y carga rápida de 68 W.", 379m, 16, "Marca=Motorola;Almacenamiento=256 GB;Memoria RAM=8 GB;Color=Azul;Pantalla=6.6\""),
                    P("Motorola Moto G84 5G", "Pantalla pOLED y sonido estéreo.", 279m, 20, "Marca=Motorola;Almacenamiento=256 GB;Memoria RAM=8 GB;Color=Azul;Pantalla=6.6\""),
                    P("Motorola Razr 40", "Plegable de bolsillo con pantalla externa útil.", 699m, 7, "Marca=Motorola;Almacenamiento=256 GB;Memoria RAM=8 GB;Color=Morado;Pantalla=6.7\""),
                    P("Xiaomi Redmi Note 13 Pro", "Cámara de 200 MP en gama media.", 329m, 24, "Marca=Xiaomi;Almacenamiento=256 GB;Memoria RAM=8 GB;Color=Dorado;Pantalla=6.7\""),
                    P("Xiaomi 13T", "Óptica Leica y carga ultrarrápida.", 549m, 12, "Marca=Xiaomi;Almacenamiento=256 GB;Memoria RAM=12 GB;Color=Azul;Pantalla=6.7\""),
                    P("POCO X6 Pro", "Rendimiento de juego a precio contenido.", 299m, 18, "Marca=Xiaomi;Almacenamiento=256 GB;Memoria RAM=8 GB;Color=Negro;Pantalla=6.7\""),
                    P("Xiaomi 14", "Compacto de gama alta con cámara Leica.", 699m, 9, "Marca=Xiaomi;Almacenamiento=256 GB;Memoria RAM=12 GB;Color=Negro;Pantalla=6.1\""),
                    P("Apple iPhone 13 Pro", "Pantalla ProMotion y sistema de tres cámaras.", 799m, 10, "Marca=Apple;Almacenamiento=256 GB;Memoria RAM=6 GB;Color=Azul;Pantalla=6.1\""),
                    P("Apple iPhone 14", "Gran cámara y modo de acción para video.", 699m, 12, "Marca=Apple;Almacenamiento=128 GB;Memoria RAM=6 GB;Color=Azul;Pantalla=6.1\""),
                    P("Apple iPhone 13", "Un clásico vigente: rápido y con excelente cámara.", 599m, 14, "Marca=Apple;Almacenamiento=128 GB;Memoria RAM=4 GB;Color=Azul;Pantalla=6.1\""),
                    P("Google Pixel 7 Pro", "La mejor fotografía computacional de Android.", 599m, 8, "Marca=Google;Almacenamiento=256 GB;Memoria RAM=12 GB;Color=Negro;Pantalla=6.7\""),
                    P("Google Pixel 7a", "Cámara de Pixel a precio razonable.", 449m, 13, "Marca=Google;Almacenamiento=128 GB;Memoria RAM=8 GB;Color=Blanco;Pantalla=6.1\""),
                    P("OPPO Find X6", "Cámara Hasselblad y acabado en cuero vegano.", 899m, 6, "Marca=OPPO;Almacenamiento=256 GB;Memoria RAM=12 GB;Color=Dorado;Pantalla=6.7\""),
                    P("realme 11 Pro+", "Cámara de 200 MP y diseño con acabado de cuero.", 399m, 15, "Marca=realme;Almacenamiento=256 GB;Memoria RAM=8 GB;Color=Verde;Pantalla=6.7\""),
                    P("Nokia G42 5G", "Resistente y reparable, con tres años de actualizaciones.", 229m, 17, "Marca=Nokia;Almacenamiento=128 GB;Memoria RAM=6 GB;Color=Negro;Pantalla=6.6\""),
                    P("OnePlus Nord CE 3", "Carga rápida de 80 W y pantalla de 120 Hz.", 349m, 14, "Marca=OnePlus;Almacenamiento=128 GB;Memoria RAM=8 GB;Color=Plata;Pantalla=6.7\""),
                    P("Honor 90", "Pantalla curva con cámara de 200 MP.", 449m, 11, "Marca=Honor;Almacenamiento=256 GB;Memoria RAM=8 GB;Color=Azul;Pantalla=6.7\""),
                    P("Sony Xperia 10 V", "Ligero, con batería enorme y jack de audífonos.", 399m, 9, "Marca=Sony;Almacenamiento=128 GB;Memoria RAM=6 GB;Color=Azul;Pantalla=6.1\""),
                    P("Huawei P30", "Fotografía nocturna destacada y diseño en degradado.", 349m, 8, "Marca=Huawei;Almacenamiento=128 GB;Memoria RAM=6 GB;Color=Plata;Pantalla=6.1\""),
                }),
            // 35 portátiles de 9 marcas (la tienda de ejemplo necesita volumen para probar filtros, paginación y rendimiento)
            new SubcategoriaEj("LAP", "Laptops", "Computadoras portátiles para estudio, trabajo y juegos",
                new[] { F("Marca", "HP", "ASUS", "Lenovo", "Dell", "Acer", "MSI", "Apple", "Samsung", "Huawei"),
                        F("Memoria RAM", "8 GB", "16 GB", "32 GB"),
                        F("Almacenamiento", "256 GB SSD", "512 GB SSD", "1 TB SSD"),
                        F("Pantalla", "13.3\"", "14\"", "15.6\"", "16\"") },
                new[]
                {
                    P("HP Pavilion 15", "Portátil versátil para el día a día, con pantalla Full HD y teclado numérico.", 649m, 18, "Marca=HP;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=15.6\""),
                    P("HP Victus 16", "Portátil gamer con gráficos dedicados y buena ventilación.", 849m, 12, "Marca=HP;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=16\""),
                    P("HP 14 Laptop", "Ligera y económica para estudiar y trabajar.", 399m, 30, "Marca=HP;Memoria RAM=8 GB;Almacenamiento=256 GB SSD;Pantalla=14\""),
                    P("HP ProBook 450 G9", "Equipo de oficina resistente y con seguridad empresarial.", 729m, 15, "Marca=HP;Memoria RAM=8 GB;Almacenamiento=256 GB SSD;Pantalla=15.6\""),
                    P("HP EliteBook 840 G9", "Ultraportátil profesional con cámara web de alta calidad.", 1199m, 9, "Marca=HP;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=14\""),
                    P("HP Envy 15", "Diseño cuidado y potencia para creación de contenido.", 999m, 10, "Marca=HP;Memoria RAM=16 GB;Almacenamiento=1 TB SSD;Pantalla=15.6\""),
                    P("ASUS VivoBook 15", "Portátil de uso general con teclado cómodo y batería de buena duración.", 519m, 24, "Marca=ASUS;Memoria RAM=8 GB;Almacenamiento=512 GB SSD;Pantalla=15.6\""),
                    P("ASUS VivoBook Go 14", "Compacta y liviana, perfecta para llevar a clases.", 429m, 22, "Marca=ASUS;Memoria RAM=8 GB;Almacenamiento=256 GB SSD;Pantalla=14\""),
                    P("ASUS ExpertBook B1", "Pensada para empresas: robusta y fácil de administrar.", 629m, 14, "Marca=ASUS;Memoria RAM=8 GB;Almacenamiento=256 GB SSD;Pantalla=14\""),
                    P("ASUS TUF Gaming F15", "Gamer con chasis reforzado y teclado retroiluminado.", 999m, 11, "Marca=ASUS;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=15.6\""),
                    P("ASUS Zenbook 14 OLED", "Pantalla OLED brillante en un cuerpo delgado y elegante.", 899m, 10, "Marca=ASUS;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=14\""),
                    P("ASUS ROG Strix G16", "Máximo rendimiento para juegos, con teclado RGB.", 1399m, 6, "Marca=ASUS;Memoria RAM=16 GB;Almacenamiento=1 TB SSD;Pantalla=16\""),
                    P("Lenovo IdeaPad 3 15", "Opción accesible para tareas escolares y de oficina.", 449m, 28, "Marca=Lenovo;Memoria RAM=8 GB;Almacenamiento=256 GB SSD;Pantalla=15.6\""),
                    P("Lenovo ThinkPad E14", "Teclado legendario y durabilidad para el trabajo diario.", 899m, 12, "Marca=Lenovo;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=14\""),
                    P("Lenovo Legion 5", "Potencia para jugar y editar, con buena disipación de calor.", 1099m, 8, "Marca=Lenovo;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=15.6\""),
                    P("Lenovo Yoga Slim 7", "Delgada y rápida, con acabado metálico.", 1049m, 10, "Marca=Lenovo;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=14\""),
                    P("Lenovo ThinkPad X1 Carbon", "Ultraligera de fibra de carbono para ejecutivos.", 1799m, 5, "Marca=Lenovo;Memoria RAM=16 GB;Almacenamiento=1 TB SSD;Pantalla=14\""),
                    P("Dell Inspiron 15 3520", "Equilibrio entre precio y desempeño para la familia.", 479m, 26, "Marca=Dell;Memoria RAM=8 GB;Almacenamiento=256 GB SSD;Pantalla=15.6\""),
                    P("Dell Vostro 3520", "Para pequeñas empresas: sólida y con buena conectividad.", 519m, 20, "Marca=Dell;Memoria RAM=8 GB;Almacenamiento=256 GB SSD;Pantalla=15.6\""),
                    P("Dell Latitude 5440", "Seguridad y movilidad para equipos de trabajo.", 1099m, 9, "Marca=Dell;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=14\""),
                    P("Dell Alienware m16", "Gamer de gama alta con pantalla de alta frecuencia.", 1999m, 4, "Marca=Dell;Memoria RAM=32 GB;Almacenamiento=1 TB SSD;Pantalla=16\""),
                    P("Dell XPS 13", "Compacta, de bordes finos y construcción premium.", 1199m, 7, "Marca=Dell;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=13.3\""),
                    P("Acer Aspire 5", "Portátil todoterreno con pantalla Full HD.", 499m, 25, "Marca=Acer;Memoria RAM=8 GB;Almacenamiento=512 GB SSD;Pantalla=15.6\""),
                    P("Acer Swift Go 14", "Ligera y silenciosa, con carga rápida.", 799m, 12, "Marca=Acer;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=14\""),
                    P("Acer Nitro 5", "Entrada al mundo gamer con excelente relación calidad-precio.", 899m, 10, "Marca=Acer;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=15.6\""),
                    P("Acer Predator Helios 16", "Rendimiento extremo para juegos exigentes.", 1699m, 5, "Marca=Acer;Memoria RAM=32 GB;Almacenamiento=1 TB SSD;Pantalla=16\""),
                    P("MSI Modern 14", "Portátil delgado para profesionales creativos.", 549m, 15, "Marca=MSI;Memoria RAM=8 GB;Almacenamiento=256 GB SSD;Pantalla=14\""),
                    P("MSI Katana 15", "Gamer con diseño agresivo y teclado retroiluminado.", 1099m, 8, "Marca=MSI;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=15.6\""),
                    P("MSI Stealth 16", "Potencia de escritorio en un chasis delgado.", 2099m, 4, "Marca=MSI;Memoria RAM=32 GB;Almacenamiento=1 TB SSD;Pantalla=16\""),
                    P("Apple MacBook Air 13", "Ligera, silenciosa y con batería para todo el día.", 999m, 14, "Marca=Apple;Memoria RAM=8 GB;Almacenamiento=256 GB SSD;Pantalla=13.3\""),
                    P("Apple MacBook Air 15", "Más pantalla sin perder portabilidad.", 1299m, 10, "Marca=Apple;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=15.6\""),
                    P("Apple MacBook Pro 14", "Para profesionales: pantalla de alto brillo y gran rendimiento.", 1599m, 8, "Marca=Apple;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=14\""),
                    P("Samsung Galaxy Book3", "Conecta fácil con tu teléfono Galaxy y es muy liviana.", 899m, 9, "Marca=Samsung;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=15.6\""),
                    P("Huawei MateBook 16", "Pantalla amplia con acabado metálico.", 849m, 8, "Marca=Huawei;Memoria RAM=16 GB;Almacenamiento=512 GB SSD;Pantalla=16\""),
                    P("Huawei MateBook 13", "Pequeña y elegante, ideal para viajar.", 699m, 6, "Marca=Huawei;Memoria RAM=8 GB;Almacenamiento=512 GB SSD;Pantalla=13.3\""),
                }),
            new SubcategoriaEj("AUD", "Audio", "Audífonos y bocinas",
                new[] { F("Tipo", "Audífonos inalámbricos", "Audífonos con cable", "Bocina portátil"), F("Marca", "Nova", "Zenith", "SoundWave"),
                        F("Color", "Negro", "Blanco", "Azul", "Rojo"), F("Conectividad", "Bluetooth", "Cable 3.5 mm", "USB-C") },
                new[]
                {
                    P("Audífonos Bluetooth con cancelación de ruido", "Hasta 30 horas de batería.", 79.90m, 33, "Tipo=Audífonos inalámbricos;Marca=SoundWave;Color=Negro;Conectividad=Bluetooth"),
                    P("Bocina portátil resistente al agua", "Sonido potente y certificación IPX7.", 45.00m, 28, "Tipo=Bocina portátil;Marca=Zenith;Color=Azul;Conectividad=Bluetooth"),
                    P("Audífonos con cable", "Clásicos, con micrófono integrado.", 12.99m, 100, "Tipo=Audífonos con cable;Marca=Nova;Color=Blanco;Conectividad=Cable 3.5 mm"),
                }),
            new SubcategoriaEj("ACC", "Accesorios", "Cargadores, cables, baterías y periféricos",
                new[] { F("Tipo", "Cargador", "Cable", "Batería externa", "Mouse"), F("Marca", "Nova", "Zenith", "Orbit"), F("Color", "Negro", "Blanco", "Gris") },
                new[]
                {
                    P("Cargador rápido USB-C de 20 W", "Carga el 50 % en 30 minutos.", 14.99m, 120, "Tipo=Cargador;Marca=Nova;Color=Blanco"),
                    P("Batería externa de 10 000 mAh", "Dos puertos de salida y carga rápida.", 24.50m, 60, "Tipo=Batería externa;Marca=Orbit;Color=Negro"),
                    P("Mouse inalámbrico", "Ergonómico, receptor USB y pilas incluidas.", 11.75m, 75, "Tipo=Mouse;Marca=Zenith;Color=Gris"),
                }),
        }),

        new("HOG", "Hogar y cocina", "Todo para equipar y decorar la casa", new[]
        {
            new SubcategoriaEj("ELE", "Electrodomésticos pequeños", "Pequeños aparatos para la cocina",
                new[] { F("Tipo", "Licuadora", "Cafetera", "Tostadora", "Freidora de aire", "Batidora"), F("Marca", "HomeChef", "Casa Viva", "Kitchen+"),
                        F("Color", "Negro", "Blanco", "Acero"), F("Potencia", "300 W", "600 W", "1000 W", "1500 W") },
                new[]
                {
                    P("Licuadora de 600 W", "Jarra de vidrio de 1.5 L y 5 velocidades.", 39.99m, 30, "Tipo=Licuadora;Marca=HomeChef;Color=Negro;Potencia=600 W"),
                    P("Freidora de aire de 4 litros", "Cocina con poco o nada de aceite.", 79.00m, 22, "Tipo=Freidora de aire;Marca=Kitchen+;Color=Negro;Potencia=1500 W"),
                    P("Cafetera de goteo de 12 tazas", "Con temporizador y plato calefactor.", 32.50m, 26, "Tipo=Cafetera;Marca=Casa Viva;Color=Acero;Potencia=1000 W"),
                }),
            new SubcategoriaEj("UTE", "Utensilios y ollas", "Para cocinar, preparar y almacenar",
                new[] { F("Material", "Acero inoxidable", "Antiadherente", "Hierro fundido", "Vidrio", "Silicona", "Bambú"), F("Marca", "HomeChef", "Casa Viva", "Kitchen+"),
                        F("Uso", "Cocción", "Preparación", "Almacenamiento") },
                new[]
                {
                    P("Set de ollas de 7 piezas", "Acero inoxidable con tapas de vidrio.", 64.90m, 18, "Material=Acero inoxidable;Marca=HomeChef;Uso=Cocción"),
                    P("Sartén antiadherente de 28 cm", "Apta para todo tipo de cocina.", 19.99m, 44, "Material=Antiadherente;Marca=Kitchen+;Uso=Cocción"),
                    P("Tabla de cortar de bambú", "Resistente y fácil de limpiar.", 11.50m, 65, "Material=Bambú;Marca=Casa Viva;Uso=Preparación"),
                }),
            new SubcategoriaEj("DEC", "Decoración", "Detalles para darle vida a los espacios",
                new[] { F("Estilo", "Moderno", "Rústico", "Minimalista"), F("Material", "Cerámica", "Madera", "Vidrio", "Tela"), F("Color", "Blanco", "Negro", "Beige", "Verde", "Azul") },
                new[]
                {
                    P("Jarrón de cerámica", "Acabado mate, 25 cm de alto.", 17.90m, 27, "Estilo=Minimalista;Material=Cerámica;Color=Blanco"),
                    P("Cojín decorativo", "Funda lavable con relleno suave.", 9.99m, 85, "Estilo=Moderno;Material=Tela;Color=Verde"),
                    P("Reloj de pared de madera", "Mecanismo silencioso, 35 cm. Descontinuado.", 21.00m, 19, "Estilo=Rústico;Material=Madera;Color=Beige", activo: false),
                }),
            new SubcategoriaEj("CAMA", "Ropa de cama", "Sábanas, edredones y almohadas",
                new[] { F("Tamaño", "Individual", "Matrimonial", "Queen", "King"), F("Material", "Algodón", "Microfibra", "Poliéster"), F("Color", "Blanco", "Gris", "Azul", "Beige"),
                        F("Clima", "Cálido", "Fresco", "Frío") },
                new[]
                {
                    P("Juego de sábanas de 4 piezas", "Algodón suave de 300 hilos.", 34.00m, 36, "Tamaño=Queen;Material=Algodón;Color=Blanco;Clima=Fresco"),
                    P("Edredón térmico", "Relleno hipoalergénico para noches frías.", 59.90m, 15, "Tamaño=Matrimonial;Material=Microfibra;Color=Gris;Clima=Frío"),
                }),
        }),

        new("DEP", "Deportes", "Equipo y accesorios para entrenar y competir", new[]
        {
            new SubcategoriaEj("FUT", "Fútbol", "Balones, calzado y protección",
                new[] { F("Tipo", "Balón", "Zapatos", "Espinilleras", "Uniforme"), F("Marca", "Goal Pro", "Stride", "TrailOne"), F("Color", "Blanco", "Negro", "Azul", "Rojo", "Verde") },
                new[]
                {
                    P("Balón de fútbol #5", "Cosido a máquina, uso en césped y cemento.", 24.99m, 48, "Tipo=Balón;Marca=Goal Pro;Color=Blanco"),
                    P("Espinilleras de protección", "Ligeras, con almohadillado de espuma.", 12.00m, 60, "Tipo=Espinilleras;Marca=Goal Pro;Color=Negro"),
                    P("Zapatos de fútbol para césped sintético", "Tacos cortos y ajuste firme.", 49.90m, 24, "Tipo=Zapatos;Marca=Stride;Color=Azul"),
                }),
            new SubcategoriaEj("FIT", "Fitness y gimnasio", "Para entrenar en casa o en el gimnasio",
                new[] { F("Tipo", "Mancuernas", "Banda elástica", "Colchoneta", "Cuerda para saltar"), F("Peso", "2 kg", "5 kg", "10 kg"), F("Marca", "Goal Pro", "Stride", "FitLife"),
                        F("Color", "Negro", "Azul", "Morado", "Verde") },
                new[]
                {
                    P("Mancuernas de 5 kg (par)", "Recubiertas de neopreno antideslizante.", 22.00m, 32, "Tipo=Mancuernas;Peso=5 kg;Marca=FitLife;Color=Negro"),
                    P("Colchoneta de yoga", "Espesor de 6 mm, antideslizante.", 18.50m, 40, "Tipo=Colchoneta;Marca=FitLife;Color=Morado"),
                    P("Set de bandas de resistencia", "Cinco niveles de resistencia con bolsa.", 13.00m, 55, "Tipo=Banda elástica;Marca=Stride;Color=Verde"),
                }),
            new SubcategoriaEj("CIC", "Ciclismo", "Seguridad y accesorios para la bicicleta",
                new[] { F("Tipo", "Casco", "Luces", "Bomba de aire", "Guantes"), F("Marca", "TrailOne", "Goal Pro", "FitLife"), F("Color", "Negro", "Rojo", "Azul"), F("Talla", "S", "M", "L") },
                new[]
                {
                    P("Casco de ciclismo", "Ventilado, ajustable y certificado.", 35.00m, 21, "Tipo=Casco;Marca=TrailOne;Color=Rojo;Talla=M"),
                    P("Luz LED recargable para bicicleta", "Tres modos de luz, carga USB.", 11.00m, 64, "Tipo=Luces;Marca=FitLife;Color=Negro;Talla=M"),
                }),
            new SubcategoriaEj("RUN", "Running y outdoor", "Hidratación y accesorios para salir a correr o caminar",
                new[] { F("Tipo", "Mochila de hidratación", "Botella térmica", "Lentes deportivos", "Gorra"), F("Marca", "TrailOne", "Stride", "FitLife"), F("Color", "Negro", "Azul", "Verde", "Naranja") },
                new[]
                {
                    P("Botella térmica de 1 litro", "Mantiene el frío hasta 24 horas.", 21.00m, 58, "Tipo=Botella térmica;Marca=FitLife;Color=Azul"),
                    P("Mochila de hidratación de 2 litros", "Ligera, con bolsillos para llaves y teléfono.", 44.00m, 16, "Tipo=Mochila de hidratación;Marca=TrailOne;Color=Verde"),
                    P("Lentes deportivos polarizados", "Protección UV400 y ajuste antideslizante.", 27.00m, 31, "Tipo=Lentes deportivos;Marca=Stride;Color=Negro"),
                }),
        }),

        new("BEL", "Belleza y cuidado personal", "Cuidado de la piel, el cabello y fragancias", new[]
        {
            new SubcategoriaEj("PIE", "Cuidado de la piel", "Limpieza, hidratación y protección",
                new[] { F("Tipo", "Limpiador", "Hidratante", "Protector solar", "Sérum"), F("Tipo de piel", "Seca", "Mixta", "Grasa", "Todo tipo"),
                        F("Marca", "Dermalia", "Aqua Pura", "Natura Sol"), F("Contenido", "50 ml", "100 ml", "200 ml") },
                new[]
                {
                    P("Protector solar FPS 50", "Textura ligera, resistente al agua.", 14.50m, 70, "Tipo=Protector solar;Tipo de piel=Todo tipo;Marca=Natura Sol;Contenido=100 ml"),
                    P("Crema hidratante facial", "Con ácido hialurónico, de rápida absorción.", 18.00m, 45, "Tipo=Hidratante;Tipo de piel=Mixta;Marca=Dermalia;Contenido=50 ml"),
                    P("Limpiador facial en gel", "Remueve impurezas sin resecar.", 11.90m, 52, "Tipo=Limpiador;Tipo de piel=Grasa;Marca=Aqua Pura;Contenido=200 ml"),
                }),
            new SubcategoriaEj("CAB", "Cuidado del cabello", "Shampoo, acondicionador y tratamientos",
                new[] { F("Tipo", "Shampoo", "Acondicionador", "Mascarilla", "Tratamiento"), F("Tipo de cabello", "Seco", "Graso", "Teñido", "Todo tipo"),
                        F("Marca", "Dermalia", "Aqua Pura", "Natura Sol"), F("Contenido", "200 ml", "400 ml", "500 ml") },
                new[]
                {
                    P("Shampoo reparador", "Fortalece y reduce el quiebre.", 8.99m, 80, "Tipo=Shampoo;Tipo de cabello=Seco;Marca=Dermalia;Contenido=400 ml"),
                    P("Mascarilla de keratina", "Tratamiento profundo semanal.", 13.50m, 34, "Tipo=Mascarilla;Tipo de cabello=Teñido;Marca=Natura Sol;Contenido=200 ml"),
                }),
            new SubcategoriaEj("FRA", "Fragancias", "Perfumes y colonias",
                new[] { F("Para", "Mujer", "Hombre", "Unisex"), F("Familia olfativa", "Cítrica", "Floral", "Amaderada", "Oriental"), F("Marca", "Aura", "Maison Sol", "Dermalia"),
                        F("Contenido", "50 ml", "100 ml") },
                new[]
                {
                    P("Eau de parfum floral de 100 ml", "Notas de jazmín y rosa.", 42.00m, 17, "Para=Mujer;Familia olfativa=Floral;Marca=Maison Sol;Contenido=100 ml"),
                    P("Colonia cítrica de 50 ml", "Fresca y revitalizante.", 25.00m, 29, "Para=Hombre;Familia olfativa=Cítrica;Marca=Aura;Contenido=50 ml"),
                    P("Perfume amaderado unisex", "Notas de cedro y ámbar. Agotado temporalmente.", 55.00m, 0, "Para=Unisex;Familia olfativa=Amaderada;Marca=Maison Sol;Contenido=100 ml"),
                }),
        }),

        new("PAP", "Papelería y escolar", "Útiles para la escuela, la universidad y la oficina", new[]
        {
            new SubcategoriaEj("CUA", "Cuadernos y libretas", "Para tomar apuntes y dibujar",
                new[] { F("Tamaño", "A5", "A4", "Carta"), F("Tipo de hoja", "Rayado", "Cuadriculado", "Blanco"), F("Marca", "Scribe", "Papel Plus"), F("Hojas", "80", "100", "200") },
                new[]
                {
                    P("Cuaderno universitario rayado", "Espiral, tapa dura, 100 hojas.", 2.75m, 200, "Tamaño=Carta;Tipo de hoja=Rayado;Marca=Scribe;Hojas=100"),
                    P("Libreta A5 cuadriculada", "Tapa flexible y cierre elástico.", 3.99m, 110, "Tamaño=A5;Tipo de hoja=Cuadriculado;Marca=Papel Plus;Hojas=80"),
                    P("Block de dibujo A4", "Papel de 120 g, ideal para lápices y acuarela.", 4.50m, 75, "Tamaño=A4;Tipo de hoja=Blanco;Marca=Scribe;Hojas=100"),
                }),
            new SubcategoriaEj("ESC", "Escritura y arte", "Bolígrafos, lápices, marcadores y colores",
                new[] { F("Tipo", "Bolígrafos", "Lápices", "Marcadores", "Colores"), F("Marca", "Scribe", "Papel Plus", "ArteViva"), F("Color", "Azul", "Negro", "Surtido"), F("Cantidad", "6", "8", "12", "24") },
                new[]
                {
                    P("Set de bolígrafos azules (12 unidades)", "Punta media, tinta de secado rápido.", 3.60m, 150, "Tipo=Bolígrafos;Marca=Scribe;Color=Azul;Cantidad=12"),
                    P("Marcadores permanentes (8 unidades)", "Punta redonda, colores surtidos.", 6.25m, 90, "Tipo=Marcadores;Marca=ArteViva;Color=Surtido;Cantidad=8"),
                    P("Colores de madera (24 unidades)", "Mina suave y pigmentada.", 7.90m, 70, "Tipo=Colores;Marca=ArteViva;Color=Surtido;Cantidad=24"),
                }),
            new SubcategoriaEj("MOC", "Mochilas y organización", "Mochilas, estuches y loncheras",
                new[] { F("Tipo", "Mochila", "Lonchera", "Estuche", "Archivador"), F("Marca", "Scribe", "Papel Plus", "TrailOne"), F("Color", "Negro", "Azul", "Rojo", "Gris"),
                        F("Capacidad", "15 L", "20 L", "30 L") },
                new[]
                {
                    P("Mochila escolar de 20 litros", "Reforzada, con compartimento para laptop.", 29.90m, 41, "Tipo=Mochila;Marca=TrailOne;Color=Azul;Capacidad=20 L"),
                    P("Estuche de doble cierre", "Amplio, con división interior.", 6.50m, 88, "Tipo=Estuche;Marca=Scribe;Color=Negro;Capacidad=15 L"),
                }),
        }),
    };
}
