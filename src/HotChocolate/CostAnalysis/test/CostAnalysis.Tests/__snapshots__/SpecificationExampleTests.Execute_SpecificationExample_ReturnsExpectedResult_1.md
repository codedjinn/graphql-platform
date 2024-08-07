# Execute_SpecificationExample_ReturnsExpectedResult

## Query

```graphql
query Example {
  topProducts
}
```

## ExpectedFieldCost

```json
5.0
```

## Result

```text
{
  "data": {
    "topProducts": null
  },
  "extensions": {
    "operationCost": {
      "fieldCost": 5,
      "typeCost": 1
    }
  }
}
```

## Schema

```text
type Query {
    topProducts(filter: Filter @cost(weight: "15.0")): [String]
        @cost(weight: "5.0") @listSize(assumedSize: 10)
}

input Filter { field: Boolean! }
```

