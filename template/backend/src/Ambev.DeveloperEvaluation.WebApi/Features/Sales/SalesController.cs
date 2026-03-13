using MediatR;
using Microsoft.AspNetCore.Mvc;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

/// <summary>
/// Controller for sale operations (create, update, cancel, get, list).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SalesController : BaseController
{
    private readonly IMediator _mediator;

    public SalesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new sale.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseWithData<CreateSaleResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSaleCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, new ApiResponseWithData<CreateSaleResult>
        {
            Success = true,
            Message = "Sale created successfully",
            Data = result
        });
    }

    /// <summary>
    /// Updates an existing sale.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseWithData<UpdateSaleResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateSaleCommand body, CancellationToken cancellationToken)
    {
        body.Id = id;
        var result = await _mediator.Send(body, cancellationToken);
        return Ok(new ApiResponseWithData<UpdateSaleResult>
        {
            Success = true,
            Message = "Sale updated successfully",
            Data = result
        });
    }

    /// <summary>
    /// Cancels a sale by id.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseWithData<CancelSaleResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var command = new CancelSaleCommand { Id = id };
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponseWithData<CancelSaleResult>
        {
            Success = true,
            Message = "Sale cancelled successfully",
            Data = result
        });
    }

    /// <summary>
    /// Cancels a specific item within a sale.
    /// </summary>
    [HttpDelete("{saleId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponseWithData<CancelSaleItemResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelItem([FromRoute] Guid saleId, [FromRoute] Guid itemId, CancellationToken cancellationToken)
    {
        var command = new CancelSaleItemCommand { SaleId = saleId, ItemId = itemId };
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponseWithData<CancelSaleItemResult>
        {
            Success = true,
            Message = "Sale item cancelled successfully",
            Data = result
        });
    }

    /// <summary>
    /// Gets a sale by id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseWithData<GetSaleResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var query = new GetSaleQuery { Id = id };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(new ApiResponseWithData<GetSaleResult>
        {
            Success = true,
            Message = "Sale retrieved successfully",
            Data = result
        });
    }

    /// <summary>
    /// Lists sales with pagination, ordering and filtering.
    /// Query params: _page (default 1), _size (default 10), _order (e.g. "saleDate desc"), and any field=value for filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseWithData<ListSalesResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var query = Request.Query;
        var page = int.TryParse(query["_page"].FirstOrDefault(), out var p) ? p : 1;
        var size = int.TryParse(query["_size"].FirstOrDefault(), out var s) ? s : 10;
        var order = query["_order"].FirstOrDefault();

        var filters = query
            .Where(kv => kv.Key != "_page" && kv.Key != "_size" && kv.Key != "_order")
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToString() ?? string.Empty);

        var listQuery = new ListSalesQuery
        {
            Page = page,
            Size = size,
            Order = order,
            Filters = filters.Count > 0 ? filters : null
        };

        var result = await _mediator.Send(listQuery, cancellationToken);
        return Ok(new ApiResponseWithData<ListSalesResult>
        {
            Success = true,
            Message = "Sales list retrieved successfully",
            Data = result
        });
    }
}
