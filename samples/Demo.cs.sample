using System.Diagnostics;
using System.Text.Json;
using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Finder;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Repository.Mongo;

namespace Domain.Infrastructure;

public class Class1
{
    public static void Main(string[] args)
    {
        IDBConnection dbConnection = new MongoConnection("localhost", 27017, "testNet");
        Monitor.Monitor monitor = Monitor.Monitor.New();
        monitor.ConfigDBConnection(dbConnection);
        monitor.RegisterLookupModel("Domain.Infrastructure","Domain.Infrastructure");
        for(int i=0;i<100;i++)
        {
            addTestData();
        }
        FindTestData();
    }

    public static void addTestData()
    {
        Test1 test1 = new Test1();
        test1.Name="Test1";
        test1.Age=18;
        test1.Gender="男";
        test1.Add();
        Test2 test2 = new Test2();
        test2.Name="Test2";
        test2.Test1Id=test1.Id;
        test2.Add();
    }
    public static void FindTestData()
    {
        var test1Data=new Finder<Test1>().List();
        Trace.WriteLine(JsonSerializer.Serialize(test1Data));
        var test2Data=new Finder<Test2>().List();
        Trace.WriteLine(JsonSerializer.Serialize(test2Data));
    }
}

public class Test1 : DomainModel<Test1>
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? Gender { get; set; }

}
[LookupModel]
public class Test2 : DomainModel<Test2>
{
    public string? Name { get; set; }
    public string? Test1Id { get; set; }
    [Lookup(FromModel =  typeof(Test1),LocalField =nameof(Test1Id),FromField = nameof(Test1.Name))]
    public string? Test1Name { get; set; }
    public int Age { get; set; }
    public string? Gender { get; set; }
}
