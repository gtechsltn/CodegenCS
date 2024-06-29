﻿class MyTemplate
{
    string myNamespace = "CodegenCS.Sample";
    string className = "MyAutogeneratedClass";
    string methodName = "HelloCodeGenerationWorld";

    FormattableString Main() => $$"""
        namespace {{myNamespace}}
        {
            {{myClass}}
        }
        """;

    FormattableString myClass() => $$"""
        public class {{className}}
        {
            {{myMethod}}
        }
        """;

    FormattableString myMethod() => $$"""
        public void {{methodName}}()
        {
            Console.WriteLine("Hello World");
        }
        """;

    // this example uses a "markup" approach (most methods are just returning text blocks)
    // but it would work similarly if you were manually calling CodegenTextWriter Write() or WriteLine()
}
