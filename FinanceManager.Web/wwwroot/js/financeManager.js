window.financeManager = {
  clickElementById: function (id) {
    try {
      var el = document.getElementById(id);
      if (el) el.click();
    } catch { }
  }
};