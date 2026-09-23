using Advocacia.BuildingBlocks.Application.PagedList;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Application.UnitTests.PagedList;

public class PagedListTests
{
    [Fact]
    public void Create_SetsItemsAndMetadata()
    {
        var items = new List<int> { 1, 2, 3 };

        var pagedList = PagedList<int>.Create(items, page: 1, pageSize: 3, totalCount: 10);

        pagedList.Items.Should().Equal(items);
        pagedList.Page.Should().Be(1);
        pagedList.PageSize.Should().Be(3);
        pagedList.TotalCount.Should().Be(10);
    }

    [Theory]
    [InlineData(10, 3, 4)]
    [InlineData(9, 3, 3)]
    [InlineData(0, 10, 0)]
    public void TotalPages_IsCalculatedFromCountAndPageSize(int totalCount, int pageSize, int expectedTotalPages)
    {
        var pagedList = PagedList<int>.Create([], 1, pageSize, totalCount);

        pagedList.TotalPages.Should().Be(expectedTotalPages);
    }

    [Fact]
    public void HasPreviousPage_WhenFirstPage_IsFalse()
    {
        var pagedList = PagedList<int>.Create([], page: 1, pageSize: 10, totalCount: 30);

        pagedList.HasPreviousPage.Should().BeFalse();
        pagedList.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_WhenLastPage_IsFalse()
    {
        var pagedList = PagedList<int>.Create([], page: 3, pageSize: 10, totalCount: 30);

        pagedList.HasNextPage.Should().BeFalse();
        pagedList.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void Empty_HasNoItemsAndZeroTotal()
    {
        var pagedList = PagedList<int>.Empty(page: 1, pageSize: 10);

        pagedList.Items.Should().BeEmpty();
        pagedList.TotalCount.Should().Be(0);
        pagedList.TotalPages.Should().Be(0);
    }
}
