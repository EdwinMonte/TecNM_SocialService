import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'
const module = process.env.PLAYWRIGHT_MODULE || 'playwright'
const { chromium } = await import(path.isAbsolute(module) ? pathToFileURL(module).href : module)
const base = process.env.UI_TEST_URL || 'https://127.0.0.1:5900'
const output = path.resolve('tmp/pdf-qa/process-access')
fs.mkdirSync(output, { recursive: true })
const browser = await chromium.launch({ channel: 'msedge', headless: true })
const errors = [], requests = [], saved = []
const codes = ['public','micro','small','medium','large','social']
let slow = false
let companies = [{ id: 10, name: 'Empresa de prueba', companyType:'small', sector:'private', companySize:'small', rfc:null, commercialName:null, isTecnmInternal:false, city:'Monclova', state:'Coahuila', address:null, postalCode:null, phoneNumber:null, email:null, isActive:true, activeSupervisorsCount:0, hasValidAgreement:false }]
async function open(role, pathname) {
  const context = await browser.newContext({ignoreHTTPSErrors:true,viewport:{width:1440,height:1000}})
  await context.addCookies([{name:'session_exp',value:String(Math.floor(Date.now()/1000)+3600),url:base,secure:true}])
  const page = await context.newPage()
  page.setDefaultTimeout(10000)
  page.on('pageerror',error=>errors.push(error.message))
  await page.route('**/api/**',async route=>{
    const url=new URL(route.request().url()), endpoint=url.pathname, method=route.request().method()
    if(!endpoint.startsWith('/api/')) return route.continue()
    requests.push({endpoint,method,params:Object.fromEntries(url.searchParams)})
    let body=[]
    if(endpoint==='/api/auth/me') body={id:1,username:role,name:'Usuario de prueba',roles:[role],email:'test@example.test'}
    else if(endpoint==='/api/academic-periods/options') body=[{id:3,label:'AD26'},{id:4,label:'FJ27'}]
    else if(endpoint.startsWith('/api/reports/')) {
      const type=url.searchParams.get('tramiteType')
      if(slow && type==='social_service') await new Promise(resolve=>setTimeout(resolve,400))
      if(endpoint==='/api/reports/statistics') body={period:{id:3,code:'AD26'},tramiteType:type,byCareer:[],totals:{},schoolYearComparison:null}
      else if(endpoint==='/api/reports/911')body={period:{id:3,code:'AD26'},tramiteType:type,byCareerAndGender:[],byCompanySector:[],byCompanySize:[],byShift:[],withDisability:{male:0,female:0,total:0},withInclusionActivities:0,totalBeneficiaries:0}
      else if(endpoint==='/api/reports/linkage') body={tramiteType:type,period:{id:3,code:'AD26'},totals:{assignedStudents:3,activeStudents:2,projects:3},byCompanyType:codes.map(code=>({companyType:code,assignedStudents:code==='small'?3:0,activeStudents:code==='small'?2:0,projects:code==='small'?3:0})),companies:{items:[{companyId:10,companyName:type==='internship'?'Empresa PP':'Dependencia SS',companyType:'small',projects:3,assignedStudents:3,activeStudents:2}],page:1,pageSize:20,totalItems:1,totalPages:1}}
    } else if(endpoint==='/api/companies' && method==='POST'){
      const payload=route.request().postDataJSON();saved.push(payload);const company={...payload,id:11,isActive:true,activeSupervisorsCount:0,hasValidAgreement:false};companies.push(company);body=company
    } else if(endpoint==='/api/companies'){
      const filtered=companies.filter(c=>!url.searchParams.get('companyType') || c.companyType===url.searchParams.get('companyType'))
      body={items:filtered,page:1,pageSize:20,totalItems:filtered.length,totalPages:1}
    } else if(endpoint==='/api/agreements') body={items:[],page:1,pageSize:20,totalItems:0,totalPages:0}
    else if(endpoint==='/api/documents/review-queue')body={items:[],page:1,totalItems:0,totalPages:0}
    else if(endpoint==='/api/students')body={items:[],page:1,totalItems:0,totalPages:0}
    await route.fulfill({status:method==='POST'?201:200,contentType:'application/json',body:JSON.stringify(body)}).catch(()=>{})
  })
  await page.goto(base+pathname)
  return {context,page}
}
try {
  for(const [role,expected,foreign] of [['social_service_officer','social_service','/internships'],['internship_coordinator','internship','/social-service']]){
    const {context,page}=await open(role,'/reports/stats')
    await page.getByText(expected==='internship'?'Empresa PP':'Dependencia SS',{exact:true}).waitFor()
    assert.equal(new URL(page.url()).pathname,'/reports/stats')
    await page.locator('select').selectOption('4')
    await page.getByText(expected==='internship'?'Empresa PP':'Dependencia SS',{exact:true}).waitFor()
    await page.getByRole('button',{name:expected==='internship'?'Estadístico Servicio Social':'Estadístico Prácticas Profesionales',exact:true}).click()
    await page.getByText(expected==='internship'?'Dependencia SS':'Empresa PP',{exact:true}).waitFor()
    assert.equal(await page.locator('select').inputValue(),'4','Process switch lost period')
    assert.ok(requests.some(r=>r.endpoint==='/api/reports/linkage' && r.params.periodId==='4' && r.params.tramiteType!==expected))
    slow=true
    await page.getByRole('button',{name:'Estadístico Servicio Social',exact:true}).click()
    await page.getByRole('button',{name:'Estadístico Prácticas Profesionales',exact:true}).click()
    await page.getByText('Empresa PP',{exact:true}).waitFor()
    await page.waitForTimeout(500)
    assert.equal(await page.getByText('Dependencia SS',{exact:true}).count(),0,'Stale statistics replaced current process')
    slow=false
    await page.screenshot({path:path.join(output,`${role}-statistics-desktop.png`),fullPage:true})
    await page.setViewportSize({width:390,height:844})
    await page.screenshot({path:path.join(output,`${role}-statistics-mobile.png`),fullPage:true})
    assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Statistics mobile overflow')
    const before=requests.length
    await page.goto(base+foreign)
    await page.getByRole('heading',{name:'Acceso no disponible'}).waitFor()
    assert.ok(!requests.slice(before).some(r=>r.endpoint==='/api'+(foreign==='/internships'?'/internships':'/social-services')),'Forbidden view requested individual data')
    await context.close()
  }
  const {context,page}=await open('social_service_officer','/companies')
  await page.getByRole('button',{name:'Registrar empresa',exact:true}).click()
  const dialog=page.getByRole('dialog')
  await dialog.getByLabel('Nombre o razón social').fill('Nueva empresa compartida')
  await dialog.getByLabel('Tipo de empresa',{exact:true}).selectOption('micro')
  await dialog.getByRole('button',{name:'Guardar empresa'}).click()
  await page.getByText('Nueva empresa compartida',{exact:true}).waitFor()
  assert.equal(saved.at(-1).companyType,'micro');assert.equal(saved.at(-1).sector,'private');assert.equal(saved.at(-1).companySize,'micro')
  await page.getByLabel('Tipo de empresa',{exact:true}).selectOption('micro')
  await page.getByText('Nueva empresa compartida',{exact:true}).waitFor()
  await page.waitForTimeout(350)
  assert.equal(await page.getByText('Empresa de prueba',{exact:true}).count(),0)
  await page.getByRole('button',{name:'Supervisores y convenios'}).click()
  await page.getByRole('button',{name:'Registrar supervisor'}).waitFor()
  await page.getByRole('button',{name:'Registrar convenio'}).waitFor()
  await page.keyboard.press('Escape')
  await page.screenshot({path:path.join(output,'companies-desktop.png'),fullPage:true})
  await page.setViewportSize({width:390,height:844})
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Company mobile overflow')
  await page.screenshot({path:path.join(output,'companies-mobile.png'),fullPage:true})
  await context.close()
  for(const pathname of ['/companies','/reports/stats','/students']){
    const {context,page}=await open('student',pathname)
    await page.getByRole('heading',{name:'Acceso no disponible'}).waitFor()
    await context.close()
  }
  const explicit=await open('internship_coordinator','/reports/stats?tramiteType=social_service')
  await explicit.page.getByText('Dependencia SS',{exact:true}).waitFor()
  await explicit.context.close()
  assert.deepEqual(errors,[])
  console.log('UI role defaults, explicit URL, process switching/races, direct-route guards, shared companies and mobile layout passed.')
} catch(error) {
  console.error('Page errors:', errors, 'Recent requests:', requests.slice(-12))
  for(const context of browser.contexts()) for(const page of context.pages()) {
    console.error('URL:',page.url(),'Body:',(await page.locator('body').innerText()).slice(0,1600))
    await page.screenshot({path:path.join(output,'failure.png'),fullPage:true})
  }
  throw error
} finally { await browser.close() }
