'use strict'

const { createGateway } = require('../proxy')
const gateway = createGateway()

module.exports = async function (context, req) {
  context.res = await gateway(req)
}
