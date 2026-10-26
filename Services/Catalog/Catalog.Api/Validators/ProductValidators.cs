using Catalog.Bll.Validation;

namespace Catalog.Api.Validators;

// Експорт валідаторів для забезпечення сумісності з автоперевіркою на рівні API та BLL
public class CreateProductDtoValidator : Catalog.Bll.Validation.CreateProductDtoValidator { }
public class UpdatePriceDtoValidator : Catalog.Bll.Validation.UpdatePriceDtoValidator { }
public class RemoveStockDtoValidator : Catalog.Bll.Validation.RemoveStockDtoValidator { }
public class AddStockDtoValidator : Catalog.Bll.Validation.AddStockDtoValidator { }
public class CreateCategoryDtoValidator : Catalog.Bll.Validation.CreateCategoryDtoValidator { }
