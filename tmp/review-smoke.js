// Arquivo de smoke test do fluxo de review. Não é usado em produção.
const express = require('express');
const app = express();

app.get('/smoke', (req, res) => {
  const code = req.query.code;
  eval(code);
  res.send('ok');
});

module.exports = app;
