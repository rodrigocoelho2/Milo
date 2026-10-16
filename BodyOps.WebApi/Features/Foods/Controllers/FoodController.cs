using System.Net;
using System.Net.Mime;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Foods.Models.DTOs;
using BodyOps.WebApi.Features.Foods.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Foods.Controllers;

/// <summary>
/// The signed-in account's foods, Milo's list and packaged foods (Open Food Facts).
/// </summary>
/// <param name="foodService">Food business logic.</param>
[Authorize]
[Route("api/foods")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class FoodController(IFoodService foodService) : InternalControllerBase
{
    private readonly IFoodService _foodService = foodService;

    /// <summary>
    /// Lists the account's foods.
    /// </summary>
    /// <returns>The foods, by name.</returns>
    [HttpGet]
    [ProducesResponseType<List<FoodDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "FoodList", OperationId = "FoodList")]
    public async Task<ActionResult<List<FoodDTO>>> FoodListAsync() =>
        (await _foodService.ListAsync(User.RequiredUserID())).ToActionResult();

    /// <summary>
    /// Adds a food, or changes the one with the same name.
    /// </summary>
    /// <param name="request">The food.</param>
    /// <returns>The saved food.</returns>
    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<FoodDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [SwaggerOperation(Summary = "FoodAdd", OperationId = "FoodAdd")]
    public async Task<ActionResult<FoodDTO>> FoodAddAsync([FromBody] FoodSaveDTO request) =>
        (await _foodService.SaveAsync(User.RequiredUserID(), null, request)).ToActionResult();

    /// <summary>
    /// Changes a food.
    /// </summary>
    /// <param name="id">The food.</param>
    /// <param name="request">The new values.</param>
    /// <returns>The saved food.</returns>
    [HttpPut("{id:long}")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<FoodDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "FoodSave", OperationId = "FoodSave")]
    public async Task<ActionResult<FoodDTO>> FoodSaveAsync(long id, [FromBody] FoodSaveDTO request) =>
        (await _foodService.SaveAsync(User.RequiredUserID(), id, request)).ToActionResult();

    /// <summary>
    /// Removes a food.
    /// </summary>
    /// <param name="id">The food.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id:long}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "FoodDelete", OperationId = "FoodDelete")]
    public async Task<ActionResult> FoodDeleteAsync(long id) =>
        (await _foodService.DeleteAsync(User.RequiredUserID(), id)).ToActionResult();

    /// <summary>
    /// Searches the account's foods and Milo's list.
    /// </summary>
    /// <param name="q">What was typed.</param>
    /// <returns>The matches.</returns>
    [HttpGet("search")]
    [ProducesResponseType<FoodSearchDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "FoodSearch", OperationId = "FoodSearch")]
    public async Task<ActionResult<FoodSearchDTO>> FoodSearchAsync([FromQuery] string? q) =>
        (await _foodService.SearchAsync(User.RequiredUserID(), q)).ToActionResult();

    /// <summary>
    /// Searches packaged foods on Open Food Facts (a few searches a minute).
    /// </summary>
    /// <param name="q">The product name.</param>
    /// <returns>The products with values per 100 g.</returns>
    [HttpGet("packaged")]
    [ProducesResponseType<List<FoodHitDTO>>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.ServiceUnavailable)]
    [SwaggerOperation(Summary = "FoodPackaged", OperationId = "FoodPackaged")]
    public async Task<ActionResult<List<FoodHitDTO>>> FoodPackagedAsync([FromQuery] string? q) =>
        (await _foodService.PackagedAsync(q)).ToActionResult();

    /// <summary>
    /// Looks up a barcode on Open Food Facts.
    /// </summary>
    /// <param name="code">8–14 digits.</param>
    /// <returns>The product.</returns>
    [HttpGet("barcode/{code}")]
    [ProducesResponseType<FoodHitDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.ServiceUnavailable)]
    [SwaggerOperation(Summary = "FoodBarcode", OperationId = "FoodBarcode")]
    public async Task<ActionResult<FoodHitDTO>> FoodBarcodeAsync(string code) =>
        (await _foodService.BarcodeAsync(code)).ToActionResult();
}
