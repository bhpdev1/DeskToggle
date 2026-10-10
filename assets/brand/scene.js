// Fills every .icons[data-cols][data-rows] container with fake desktop icons.
// Deterministic (no Math.random) so renders are reproducible.
(function () {
  var kinds = ["folder", "doc", "app1", "folder", "app2", "doc", "app3", "folder", "app4", "doc"];
  var widths = [30, 22, 34, 26, 18, 30, 24, 28];
  document.querySelectorAll(".icons[data-cols]").forEach(function (box) {
    var cols = +box.dataset.cols, rows = +box.dataset.rows;
    var holes = (box.dataset.holes || "").split(",").filter(Boolean).map(Number);
    box.style.gridTemplateColumns = "repeat(" + cols + ", 1fr)";
    box.style.gridTemplateRows = "repeat(" + rows + ", auto)";
    box.style.gridAutoFlow = "column";
    for (var n = 0; n < cols * rows; n++) {
      var cell = document.createElement("div");
      cell.className = "ic";
      if (holes.indexOf(n) === -1) {
        var k = kinds[(n * 7 + 3) % kinds.length];
        cell.innerHTML = '<i class="' + k + '"></i><b style="width:' + widths[n % widths.length] + 'px"></b>';
      }
      box.appendChild(cell);
    }
  });
})();
