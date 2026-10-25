using System.Reflection;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;
using NetArchTest.Rules;

namespace GestionPedidos.Architecture.Tests;

/// <summary>Reglas de dependencias de docs/Arquitectura.md §2: se verifican en cada ejecución de pruebas.</summary>
public class ReglasDeArquitecturaTests
{
    private static readonly Assembly Dominio = typeof(Usuario).Assembly;
    private static readonly Assembly Aplicacion = typeof(GestionPedidos.Application.Common.Resultado).Assembly;
    private static readonly Assembly Infraestructura = typeof(GestionPedidos.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Interfaz = typeof(GestionPedidos.Ui.UiOptions).Assembly;
    private static readonly Assembly Contratos = typeof(Permisos).Assembly;

    private static IEnumerable<string> Referencias(Assembly a) => a.GetReferencedAssemblies().Select(r => r.Name!);

    [Fact]
    public void Domain_NoDependeDeNada_DelProyectoNiDeInfraestructura()
    {
        var malos = new[] { "GestionPedidos.Application", "GestionPedidos.Infrastructure", "GestionPedidos.Api", "GestionPedidos.Ui", "GestionPedidos.Contracts",
                            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore" };
        var r = Types.InAssembly(Dominio).ShouldNot().HaveDependencyOnAny(malos).GetResult();
        Assert.True(r.IsSuccessful, "Domain depende de: " + string.Join(", ", r.FailingTypeNames ?? Array.Empty<string>()));
        Assert.DoesNotContain(Referencias(Dominio), n => n.StartsWith("GestionPedidos") || n.StartsWith("Microsoft.EntityFrameworkCore"));
    }

    [Fact]
    public void Application_SoloConoceDomainYContracts_NuncaEFCoreNiInfraestructura()
    {
        var r = Types.InAssembly(Aplicacion).ShouldNot()
            .HaveDependencyOnAny("GestionPedidos.Infrastructure", "GestionPedidos.Api", "GestionPedidos.Ui", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore").GetResult();
        Assert.True(r.IsSuccessful, "Application depende de: " + string.Join(", ", r.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ui_NoConoceElDominioNiLaAplicacionNiLaInfraestructura_SoloContracts()
    {
        var referencias = Referencias(Interfaz).ToList();
        Assert.DoesNotContain("GestionPedidos.Domain", referencias);
        Assert.DoesNotContain("GestionPedidos.Application", referencias);
        Assert.DoesNotContain("GestionPedidos.Infrastructure", referencias);
        Assert.DoesNotContain("GestionPedidos.Api", referencias);
        Assert.DoesNotContain(referencias, n => n.StartsWith("Microsoft.EntityFrameworkCore"));
        Assert.Contains("GestionPedidos.Contracts", referencias);
    }

    [Fact]
    public void Contracts_NoTieneDependenciasDelProyecto() =>
        Assert.DoesNotContain(Referencias(Contratos), n => n.StartsWith("GestionPedidos"));

    [Fact]
    public void Infraestructura_EsLaUnicaQueUsaEFCore()
    {
        Assert.Contains(Referencias(Infraestructura), n => n.StartsWith("Microsoft.EntityFrameworkCore"));
        Assert.DoesNotContain(Referencias(Aplicacion), n => n.StartsWith("Microsoft.EntityFrameworkCore"));
        Assert.DoesNotContain(Referencias(Interfaz), n => n.StartsWith("Microsoft.EntityFrameworkCore"));
    }

    [Fact]
    public void ViewModels_NoDependenDeBlazor()
    {
        var r = Types.InAssembly(Interfaz).That().HaveNameEndingWith("ViewModel").ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.Components").GetResult();
        Assert.True(r.IsSuccessful, "ViewModels que dependen de Blazor: " + string.Join(", ", r.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void LosPermisosDeLaInterfaz_CoincidenConElCatalogoDelDominio()
    {
        // Contracts.Permisos es una copia para que la Ui no conozca el dominio: este test impide que diverjan.
        var delContrato = typeof(Permisos).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        var delDominio = CatalogoPermisos.Todos.Select(p => p.Codigo).ToHashSet();
        Assert.True(delContrato.IsSubsetOf(delDominio), "Permisos de la Ui que no existen en el dominio: " + string.Join(", ", delContrato.Except(delDominio)));
    }

    [Fact]
    public void LosEventosSiguenElPatronDeNombres()
    {
        // Todo delegado de evento termina en EventHandler y todo EventArgs hereda de EventArgs (pautas de código).
        var delegados = Types.InAssemblies(new[] { Aplicacion, Interfaz }).That().Inherit(typeof(MulticastDelegate)).GetTypes();
        Assert.All(delegados.Where(t => t.Name != "ReglaDeValidacion`1"), t => Assert.EndsWith("EventHandler", t.Name));
        var args = Types.InAssemblies(new[] { Aplicacion, Interfaz }).That().HaveNameEndingWith("EventArgs").GetTypes();
        Assert.All(args, t => Assert.True(typeof(EventArgs).IsAssignableFrom(t), t.Name));
    }
}
